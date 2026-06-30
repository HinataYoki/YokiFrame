#if !GODOT
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 命令桥驱动壳（仅编辑器）。
    /// </summary>
    [InitializeOnLoad]
    internal static partial class UnityCommandBridgeHost
    {
        private const int HEARTBEAT_INTERVAL_MS = 2000;
        private const int POLL_MIN_INTERVAL_MS = 100;
        private const int POLL_MAX_INTERVAL_MS = 1000;
        private const int KIT_SNAPSHOT_INTERVAL_MS = 1000;
        private const string ENGINE_ID = "unity-editor";
        private const string BRIDGE_UNAVAILABLE_JSON = "{\"available\":false,\"reason\":\"core is not initialized\"}";
        private const string SAVEKIT_COMMAND_HANDLER_TYPE = "YokiFrame.SaveKitCommandHandler, YokiFrame.SaveKit";
        private const string LOCALIZATIONKIT_COMMAND_HANDLER_TYPE = "YokiFrame.LocalizationKitCommandHandler, YokiFrame.LocalizationKit";
        private const string SCENEKIT_COMMAND_HANDLER_TYPE = "YokiFrame.SceneKitCommandHandler, YokiFrame.SceneKit";
        private const string SPATIALKIT_COMMAND_HANDLER_TYPE = "YokiFrame.SpatialKitCommandHandler, YokiFrame.SpatialKit";
        private const string INPUTKIT_COMMAND_HANDLER_TYPE = "YokiFrame.InputKitCommandHandler, YokiFrame.InputKit";
        private const string UIKIT_COMMAND_HANDLER_TYPE = "YokiFrame.UnityUIKitCommandHandler, YokiFrame.UIKit.Editor";
        private const string ACTIONKIT_COMMAND_HANDLER_TYPE = "YokiFrame.ActionKitCommandHandler, YokiFrame.ActionKit";

        private static YokiCommandBridgeCore sEngineCore;
        private static string sYokiframeRoot;
        private static DateTime sLastHeartbeat;
        private static DateTime? sLastPollUtc;
        private static DateTime? sLastKitSnapshotPublishUtc;
        private static readonly string sStartedAtUtc = DateTime.UtcNow.ToString("O");
        private static readonly CommandBridgePollBackoff sPollBackoff =
            new CommandBridgePollBackoff(POLL_MIN_INTERVAL_MS, POLL_MAX_INTERVAL_MS);
        private static FileSystemWatcher sCommandDirectoryWatcher;
        private static volatile bool sCommandDirectoryChanged;

        /// <summary>
        /// engine-scoped 文件桥使用的共享命令分发器。
        /// </summary>
        public static KitCommandDispatcher Dispatcher { get; private set; }

        static UnityCommandBridgeHost()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            sYokiframeRoot = Path.Combine(projectRoot, ".yokiframe");

            Dispatcher = new KitCommandDispatcher();
            EnsureDefaultLogger();
            sEngineCore = CreateEngineCore(sYokiframeRoot, Dispatcher);

            EnsureRuntimeSettingsStore();
            EnsureDefaultResourceProvider();
            KitStateSnapshotPublisher.RestoreAndPublishPoolMonitorPreferences(sYokiframeRoot);

            RegisterCommandHandlers();
            UnityEventStreamWriter.Init(sYokiframeRoot);
            ResetCommandDirectoryWatcher();

            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeCommandDirectoryWatcher;
            EditorApplication.quitting += DisposeCommandDirectoryWatcher;

            WriteEngineRegistry();
            PollCores();
        }

        private static void RegisterCommandHandlers()
        {
            Dispatcher.Register(new SystemCommandHandler(
                () => BuildBridgeStatusJson(sEngineCore),
                OpenCodeLocationWithDefaultEditor,
                () => Dispatcher != null ? Dispatcher.BuildCommandCatalogJson() : BRIDGE_UNAVAILABLE_JSON,
                () => BuildBridgeStatusDetailJson(sEngineCore)));
            Dispatcher.Register(new FsmKitCommandHandler());
            Dispatcher.Register(new UnityPoolKitCommandHandler());
            Dispatcher.Register(new UnityLogKitCommandHandler());
            Dispatcher.Register(new ResKitCommandHandler());
            Dispatcher.Register(new EventKitCommandHandler());
            Dispatcher.Register(new SingletonKitCommandHandler());
            Dispatcher.Register(new ArchitectureCommandHandler());
            UnityManagedRuntimeBackendRegistration.EnsureRegistered();
            Dispatcher.Register(new ManagedRuntimeKitCommandHandler());
            Dispatcher.Register(new AudioKitCommandHandler());
            RegisterOptionalToolCommandHandlers();
        }

        private static void RegisterOptionalToolCommandHandlers()
        {
            // Tools Kit 可以独立安装，命令桥只按类型名尝试挂载，避免 Editor Adapter 反向硬依赖每个 Kit。
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, SAVEKIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, LOCALIZATIONKIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, SCENEKIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, SPATIALKIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, INPUTKIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, UIKIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(Dispatcher, ACTIONKIT_COMMAND_HANDLER_TYPE);
        }

        private static void EnsureDefaultResourceProvider()
        {
            if (ResKit.GetProvider() != default)
                return;

            ResKit.SetProvider(new UnityResourceProvider());
        }

        private static void EnsureDefaultLogger()
        {
            if (LogKit.HasLogger)
                return;

            LogKit.SetLogger(new UnityEngineLogger());
        }

        private static void EnsureRuntimeSettingsStore()
        {
            // UnityRuntimeSettingsBridge 会安装 UnityRuntimeKitSettingsStore，让 Tauri 写入的 Kit 设置持久化到 Runtime Settings 资产。
            UnityRuntimeSettingsBridge.EnsureInstalled();
            UnityLogKitRuntimeInstaller.Install(
                UnityRuntimeSettingsBridge.GetLogKitOptions(UnityLogKitOptions.CreateDefault()),
                LogKit.GetLogger());
        }

        private static void OnEditorUpdate()
        {
            EnsureDefaultLogger();
            UnityManagedRuntimeBackendRegistration.EnsureRegistered();

            var nowUtc = DateTime.UtcNow;
            if (ShouldPollCommandBridge(nowUtc))
            {
                sCommandDirectoryChanged = false;
                PollCores();
                sPollBackoff.RecordPollResult(sEngineCore != default &&
                    (sEngineCore.LastPollHadActivity || sEngineCore.BackpressureActive));
                sLastPollUtc = nowUtc;
            }

            if (ShouldPoll(nowUtc, sLastKitSnapshotPublishUtc, TimeSpan.FromMilliseconds(KIT_SNAPSHOT_INTERVAL_MS)))
            {
                KitStateSnapshotPublisher.TryPublishAll(sYokiframeRoot);
                sLastKitSnapshotPublishUtc = nowUtc;
            }

            if ((nowUtc - sLastHeartbeat).TotalMilliseconds >= HEARTBEAT_INTERVAL_MS)
            {
                sLastHeartbeat = nowUtc;
                WriteHeartbeat();
            }
        }

        internal static bool ShouldPoll(DateTime nowUtc, DateTime? lastPollUtc, TimeSpan interval)
        {
            if (!lastPollUtc.HasValue)
                return true;

            if (interval <= TimeSpan.Zero)
                return true;

            return nowUtc - lastPollUtc.Value >= interval;
        }

        private static bool ShouldPollCommandBridge(DateTime nowUtc)
        {
            if (sCommandDirectoryChanged)
            {
                sPollBackoff.Reset();
                return true;
            }

            return ShouldPoll(nowUtc, sLastPollUtc, TimeSpan.FromMilliseconds(sPollBackoff.CurrentIntervalMs));
        }

        public static void PollNow()
        {
            if (sEngineCore != default)
            {
                PollCores();
                sPollBackoff.RecordPollResult(sEngineCore.LastPollHadActivity || sEngineCore.BackpressureActive);
                sLastPollUtc = DateTime.UtcNow;
                return;
            }

            LogKit.Warning("[YokiCommandBridge] 未初始化，请等待 DomainReload 完成后重试");
        }

        private static void ResetCommandDirectoryWatcher()
        {
            DisposeCommandDirectoryWatcher();

            try
            {
                var commandDir = Path.Combine(GetEngineRoot(sYokiframeRoot), "commands");
                Directory.CreateDirectory(commandDir);
                sCommandDirectoryWatcher = new FileSystemWatcher(commandDir, "*.json");
                sCommandDirectoryWatcher.IncludeSubdirectories = false;
                sCommandDirectoryWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                sCommandDirectoryWatcher.Created += OnCommandDirectoryChanged;
                sCommandDirectoryWatcher.Changed += OnCommandDirectoryChanged;
                sCommandDirectoryWatcher.Renamed += OnCommandDirectoryChanged;
                sCommandDirectoryWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                LogKit.Warning("[YokiCommandBridge] 命令目录监听启动失败，回退到自适应轮询: " + e.Message);
            }
        }

        private static void OnCommandDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            sCommandDirectoryChanged = true;
        }

        private static void DisposeCommandDirectoryWatcher()
        {
            if (sCommandDirectoryWatcher == null)
                return;

            sCommandDirectoryWatcher.EnableRaisingEvents = false;
            sCommandDirectoryWatcher.Created -= OnCommandDirectoryChanged;
            sCommandDirectoryWatcher.Changed -= OnCommandDirectoryChanged;
            sCommandDirectoryWatcher.Renamed -= OnCommandDirectoryChanged;
            sCommandDirectoryWatcher.Dispose();
            sCommandDirectoryWatcher = null;
        }
    }
}
#endif
