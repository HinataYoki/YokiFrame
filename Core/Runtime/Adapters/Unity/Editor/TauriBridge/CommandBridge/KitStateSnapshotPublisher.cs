#if !GODOT
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// PoolKit、ResKit、SingletonKit、AudioKit、LogKit、SaveKit、LocalizationKit、SceneKit、SpatialKit、InputKit、UIKit 的编辑器快照发布器。
    /// 这些 Kit 没有专用运行时推送事件，因此在 Editor update 里节流汇总当前状态，避免热路径写文件。
    /// </summary>
    internal static class KitStateSnapshotPublisher
    {
        private const string ENGINE_ID = "unity-editor";
        private const string SNAPSHOT_NAME = "state";
        private const string POOL_KIT_NAME = "PoolKit";
        private const string RES_KIT_NAME = "ResKit";
        private const string SINGLETON_KIT_NAME = "SingletonKit";
        private const string AUDIO_KIT_NAME = "AudioKit";
        private const string LOG_KIT_NAME = "LogKit";
        private const string SAVE_KIT_NAME = "SaveKit";
        private const string SAVE_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SaveKitCommandHandler, YokiFrame.SaveKit";
        private const string LOCALIZATION_KIT_NAME = "LocalizationKit";
        private const string LOCALIZATION_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.LocalizationKitCommandHandler, YokiFrame.LocalizationKit";
        private const string SCENE_KIT_NAME = "SceneKit";
        private const string SCENE_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SceneKitCommandHandler, YokiFrame.SceneKit";
        private const string SPATIAL_KIT_NAME = "SpatialKit";
        private const string SPATIAL_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SpatialKitCommandHandler, YokiFrame.SpatialKit";
        private const string INPUT_KIT_NAME = "InputKit";
        private const string INPUT_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.InputKitCommandHandler, YokiFrame.InputKit";
        private const string UI_KIT_NAME = "UIKit";
        private const string UI_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.UIKitCommandHandler, YokiFrame.UIKit";
        private const string ACTION_KIT_NAME = "ActionKit";
        private const string ACTION_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.ActionKitCommandHandler, YokiFrame.ActionKit";
        private const string POOL_TRACKING_PREF_KEY = "YokiFrame.PoolKit.EnableTracking";
        private const string POOL_EVENT_HISTORY_PREF_KEY = "YokiFrame.PoolKit.EnableEventHistory";
        private const string POOL_STACK_TRACE_PREF_KEY = "YokiFrame.PoolKit.EnableStackTrace";

        private static readonly PoolKitCommandHandler sPoolHandler = new PoolKitCommandHandler();
        private static readonly ResKitCommandHandler sResHandler = new ResKitCommandHandler();
        private static readonly SingletonKitCommandHandler sSingletonHandler = new SingletonKitCommandHandler();
        private static readonly AudioKitCommandHandler sAudioHandler = new AudioKitCommandHandler();
        private static readonly LogKitCommandHandler sLogHandler = new LogKitCommandHandler();
        private static IKitCommandHandler sSaveHandler;
        private static IKitCommandHandler sLocalizationHandler;
        private static IKitCommandHandler sSceneHandler;
        private static IKitCommandHandler sSpatialHandler;
        private static IKitCommandHandler sInputHandler;
        private static IKitCommandHandler sUIKitHandler;
        private static IKitCommandHandler sActionHandler;

        private static readonly CommandBridgeSnapshotPublisher sPoolPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, POOL_KIT_NAME, SNAPSHOT_NAME, BuildPoolPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sResPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, RES_KIT_NAME, SNAPSHOT_NAME, BuildResPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sSingletonPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SINGLETON_KIT_NAME, SNAPSHOT_NAME, BuildSingletonPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sAudioPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, AUDIO_KIT_NAME, SNAPSHOT_NAME, BuildAudioPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sLogPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, LOG_KIT_NAME, SNAPSHOT_NAME, BuildLogPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sSavePublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SAVE_KIT_NAME, SNAPSHOT_NAME, BuildSavePayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sLocalizationPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, LOCALIZATION_KIT_NAME, SNAPSHOT_NAME, BuildLocalizationPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sScenePublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SCENE_KIT_NAME, SNAPSHOT_NAME, BuildScenePayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sSpatialPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SPATIAL_KIT_NAME, SNAPSHOT_NAME, BuildSpatialPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sInputPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, INPUT_KIT_NAME, SNAPSHOT_NAME, BuildInputPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sUIKitPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, UI_KIT_NAME, SNAPSHOT_NAME, BuildUIKitPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sActionPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, ACTION_KIT_NAME, SNAPSHOT_NAME, BuildActionPayloadJson);

        private static string sLastPoolPayloadJson;
        private static string sLastPoolInvalidationKey;
        private static string sLastResPayloadJson;
        private static string sLastResInvalidationKey;
        private static string sLastSingletonPayloadJson;
        private static string sLastSingletonInvalidationKey;
        private static string sLastAudioPayloadJson;
        private static string sLastAudioInvalidationKey;
        private static string sLastLogPayloadJson;
        private static string sLastLogInvalidationKey;
        private static string sLastSavePayloadJson;
        private static string sLastSaveInvalidationKey;
        private static string sLastLocalizationPayloadJson;
        private static string sLastLocalizationInvalidationKey;
        private static string sLastScenePayloadJson;
        private static string sLastSceneInvalidationKey;
        private static string sLastSpatialPayloadJson;
        private static string sLastSpatialInvalidationKey;
        private static string sLastInputPayloadJson;
        private static string sLastInputInvalidationKey;
        private static string sLastUIKitPayloadJson;
        private static string sLastUIKitInvalidationKey;
        private static string sLastActionPayloadJson;
        private static string sLastActionInvalidationKey;

        static KitStateSnapshotPublisher()
        {
            RestorePoolMonitorPreferences();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void TryPublishAll(string yokiframeRoot)
        {
            PublishIfInvalidated(yokiframeRoot, POOL_KIT_NAME, sPoolPublisher, BuildPoolPayloadJson, sPoolHandler, ref sLastPoolPayloadJson, ref sLastPoolInvalidationKey, false);
            PublishIfInvalidated(yokiframeRoot, RES_KIT_NAME, sResPublisher, BuildResPayloadJson, sResHandler, ref sLastResPayloadJson, ref sLastResInvalidationKey, false);
            PublishIfInvalidated(yokiframeRoot, SINGLETON_KIT_NAME, sSingletonPublisher, BuildSingletonPayloadJson, sSingletonHandler, ref sLastSingletonPayloadJson, ref sLastSingletonInvalidationKey, false);
            PublishIfInvalidated(yokiframeRoot, AUDIO_KIT_NAME, sAudioPublisher, BuildAudioPayloadJson, sAudioHandler, ref sLastAudioPayloadJson, ref sLastAudioInvalidationKey, false);
            PublishIfInvalidated(yokiframeRoot, LOG_KIT_NAME, sLogPublisher, BuildLogPayloadJson, sLogHandler, ref sLastLogPayloadJson, ref sLastLogInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, SAVE_KIT_NAME, sSavePublisher, EnsureSaveHandler, () => sSaveHandler, BuildSavePayloadJson, ref sLastSavePayloadJson, ref sLastSaveInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, LOCALIZATION_KIT_NAME, sLocalizationPublisher, EnsureLocalizationHandler, () => sLocalizationHandler, BuildLocalizationPayloadJson, ref sLastLocalizationPayloadJson, ref sLastLocalizationInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, SCENE_KIT_NAME, sScenePublisher, EnsureSceneHandler, () => sSceneHandler, BuildScenePayloadJson, ref sLastScenePayloadJson, ref sLastSceneInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, SPATIAL_KIT_NAME, sSpatialPublisher, EnsureSpatialHandler, () => sSpatialHandler, BuildSpatialPayloadJson, ref sLastSpatialPayloadJson, ref sLastSpatialInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, INPUT_KIT_NAME, sInputPublisher, EnsureInputHandler, () => sInputHandler, BuildInputPayloadJson, ref sLastInputPayloadJson, ref sLastInputInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, UI_KIT_NAME, sUIKitPublisher, EnsureUIKitHandler, () => sUIKitHandler, BuildUIKitPayloadJson, ref sLastUIKitPayloadJson, ref sLastUIKitInvalidationKey, false);
            PublishOptionalIfInvalidated(yokiframeRoot, ACTION_KIT_NAME, sActionPublisher, EnsureActionHandler, () => sActionHandler, BuildActionPayloadJson, ref sLastActionPayloadJson, ref sLastActionInvalidationKey, false);
        }

        public static void ForcePublishAll(string yokiframeRoot)
        {
            PublishIfInvalidated(yokiframeRoot, POOL_KIT_NAME, sPoolPublisher, BuildPoolPayloadJson, sPoolHandler, ref sLastPoolPayloadJson, ref sLastPoolInvalidationKey, true);
            PublishIfInvalidated(yokiframeRoot, RES_KIT_NAME, sResPublisher, BuildResPayloadJson, sResHandler, ref sLastResPayloadJson, ref sLastResInvalidationKey, true);
            PublishIfInvalidated(yokiframeRoot, SINGLETON_KIT_NAME, sSingletonPublisher, BuildSingletonPayloadJson, sSingletonHandler, ref sLastSingletonPayloadJson, ref sLastSingletonInvalidationKey, true);
            PublishIfInvalidated(yokiframeRoot, AUDIO_KIT_NAME, sAudioPublisher, BuildAudioPayloadJson, sAudioHandler, ref sLastAudioPayloadJson, ref sLastAudioInvalidationKey, true);
            PublishIfInvalidated(yokiframeRoot, LOG_KIT_NAME, sLogPublisher, BuildLogPayloadJson, sLogHandler, ref sLastLogPayloadJson, ref sLastLogInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, SAVE_KIT_NAME, sSavePublisher, EnsureSaveHandler, () => sSaveHandler, BuildSavePayloadJson, ref sLastSavePayloadJson, ref sLastSaveInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, LOCALIZATION_KIT_NAME, sLocalizationPublisher, EnsureLocalizationHandler, () => sLocalizationHandler, BuildLocalizationPayloadJson, ref sLastLocalizationPayloadJson, ref sLastLocalizationInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, SCENE_KIT_NAME, sScenePublisher, EnsureSceneHandler, () => sSceneHandler, BuildScenePayloadJson, ref sLastScenePayloadJson, ref sLastSceneInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, SPATIAL_KIT_NAME, sSpatialPublisher, EnsureSpatialHandler, () => sSpatialHandler, BuildSpatialPayloadJson, ref sLastSpatialPayloadJson, ref sLastSpatialInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, INPUT_KIT_NAME, sInputPublisher, EnsureInputHandler, () => sInputHandler, BuildInputPayloadJson, ref sLastInputPayloadJson, ref sLastInputInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, UI_KIT_NAME, sUIKitPublisher, EnsureUIKitHandler, () => sUIKitHandler, BuildUIKitPayloadJson, ref sLastUIKitPayloadJson, ref sLastUIKitInvalidationKey, true);
            PublishOptionalIfInvalidated(yokiframeRoot, ACTION_KIT_NAME, sActionPublisher, EnsureActionHandler, () => sActionHandler, BuildActionPayloadJson, ref sLastActionPayloadJson, ref sLastActionInvalidationKey, true);
        }

        public static void RestoreAndPublishPoolMonitorPreferences(string yokiframeRoot)
        {
            RestorePoolMonitorPreferences();
            sLastPoolPayloadJson = null;
            PublishIfChanged(yokiframeRoot, POOL_KIT_NAME, sPoolPublisher, BuildPoolPayloadJson, ref sLastPoolPayloadJson, true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RestorePoolMonitorPreferences();
                ForcePublishAll(GetDefaultYokiframeRoot());
                return;
            }

            if (state != PlayModeStateChange.ExitingPlayMode && state != PlayModeStateChange.EnteredEditMode)
                return;

            PoolDebugger.ClearRuntimeMonitorState();
            RestorePoolMonitorPreferences();
            sLastPoolPayloadJson = null;
            ForcePublishAll(GetDefaultYokiframeRoot());
        }

        private static void PublishIfChanged(
            string yokiframeRoot,
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<string> payloadFactory,
            ref string lastPayloadJson,
            bool force)
        {
            if (string.IsNullOrEmpty(yokiframeRoot))
                return;

            try
            {
                var payloadJson = payloadFactory();
                if (!force && string.Equals(payloadJson, lastPayloadJson, StringComparison.Ordinal))
                    return;

                publisher.Publish(yokiframeRoot, payloadJson);
                UnitySharedMemoryTelemetry.TryWriteLatest(ENGINE_ID, kitName, SNAPSHOT_NAME, payloadJson);
                lastPayloadJson = payloadJson;
            }
            catch (Exception e)
            {
                LogKit.Warning("[KitStateSnapshotPublisher] 写入 " + kitName + " snapshot 失败: " + e.Message);
            }
        }

        private static void PublishIfInvalidated(
            string yokiframeRoot,
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<string> payloadFactory,
            IKitSnapshotInvalidationProvider invalidationProvider,
            ref string lastPayloadJson,
            ref string lastInvalidationKey,
            bool force)
        {
            if (invalidationProvider == null)
            {
                PublishIfChanged(yokiframeRoot, kitName, publisher, payloadFactory, ref lastPayloadJson, force);
                return;
            }

            if (string.IsNullOrEmpty(yokiframeRoot))
                return;

            try
            {
                var invalidationKey = invalidationProvider.GetSnapshotInvalidationKey();
                if (!force && string.Equals(invalidationKey, lastInvalidationKey, StringComparison.Ordinal))
                    return;

                var payloadJson = payloadFactory();
                if (!force && string.Equals(payloadJson, lastPayloadJson, StringComparison.Ordinal))
                {
                    lastInvalidationKey = invalidationKey;
                    return;
                }

                publisher.Publish(yokiframeRoot, payloadJson);
                UnitySharedMemoryTelemetry.TryWriteLatest(ENGINE_ID, kitName, SNAPSHOT_NAME, payloadJson);
                lastPayloadJson = payloadJson;
                lastInvalidationKey = invalidationKey;
            }
            catch (Exception e)
            {
                LogKit.Warning("[KitStateSnapshotPublisher] 写入 " + kitName + " snapshot 失败: " + e.Message);
            }
        }

        private static void PublishOptionalIfChanged(
            string yokiframeRoot,
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<bool> ensureHandler,
            Func<string> payloadFactory,
            ref string lastPayloadJson,
            bool force)
        {
            if (!ensureHandler())
                return;

            PublishIfChanged(yokiframeRoot, kitName, publisher, payloadFactory, ref lastPayloadJson, force);
        }

        private static void PublishOptionalIfInvalidated(
            string yokiframeRoot,
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<bool> ensureHandler,
            Func<IKitCommandHandler> getHandler,
            Func<string> payloadFactory,
            ref string lastPayloadJson,
            ref string lastInvalidationKey,
            bool force)
        {
            if (!ensureHandler())
                return;

            PublishIfInvalidated(
                yokiframeRoot,
                kitName,
                publisher,
                payloadFactory,
                getHandler() as IKitSnapshotInvalidationProvider,
                ref lastPayloadJson,
                ref lastInvalidationKey,
                force);
        }

        private static bool EnsureSaveHandler()
        {
            if (sSaveHandler != null)
                return true;

            // SaveKit 是 Tools 层可选模块；这里通过 Base 共用 helper 创建 handler，保持 Adapter 层不硬引用 Tools 程序集。
            return OptionalKitCommandHandlerRegistry.TryCreate(SAVE_KIT_COMMAND_HANDLER_TYPE, out sSaveHandler);
        }

        private static bool EnsureLocalizationHandler()
        {
            if (sLocalizationHandler != null)
                return true;

            // LocalizationKit 是 Tools 层可选模块；复用可选 handler 注册，避免每个宿主单独写一套反射胶水。
            return OptionalKitCommandHandlerRegistry.TryCreate(LOCALIZATION_KIT_COMMAND_HANDLER_TYPE, out sLocalizationHandler);
        }

        private static bool EnsureSceneHandler()
        {
            if (sSceneHandler != null)
                return true;

            // SceneKit 是 Tools 层可选模块；继续复用同一套 optional handler 和 snapshot 发布链路。
            return OptionalKitCommandHandlerRegistry.TryCreate(SCENE_KIT_COMMAND_HANDLER_TYPE, out sSceneHandler);
        }

        private static bool EnsureSpatialHandler()
        {
            if (sSpatialHandler != null)
                return true;

            // SpatialKit 是纯 C# Tools 模块；通过同一套 optional handler 接入，避免 Adapter 硬引用 Tools 程序集。
            return OptionalKitCommandHandlerRegistry.TryCreate(SPATIAL_KIT_COMMAND_HANDLER_TYPE, out sSpatialHandler);
        }

        private static bool EnsureInputHandler()
        {
            if (sInputHandler != null)
                return true;

            // InputKit 后端由宿主安装，诊断 handler 仍通过 optional helper 接入，避免 Unity/Godot 各写一套反射胶水。
            return OptionalKitCommandHandlerRegistry.TryCreate(INPUT_KIT_COMMAND_HANDLER_TYPE, out sInputHandler);
        }

        private static bool EnsureUIKitHandler()
        {
            if (sUIKitHandler != null)
                return true;

            // UIKit 后端由宿主安装，快照只读观察面板栈；通过 optional helper 保持 Adapter 不硬引用 Tools。
            return OptionalKitCommandHandlerRegistry.TryCreate(UI_KIT_COMMAND_HANDLER_TYPE, out sUIKitHandler);
        }

        private static bool EnsureActionHandler()
        {
            if (sActionHandler != null)
                return true;

            return OptionalKitCommandHandlerRegistry.TryCreate(ACTION_KIT_COMMAND_HANDLER_TYPE, out sActionHandler);
        }

        private static string BuildPoolPayloadJson()
        {
            return sPoolHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        internal static string ApplyPoolTrackingCommand(string payloadJson)
        {
            var result = sPoolHandler.HandleAction("set_tracking", payloadJson);
            SavePoolMonitorPreferences();
            sLastPoolPayloadJson = null;
            PublishIfChanged(GetDefaultYokiframeRoot(), POOL_KIT_NAME, sPoolPublisher, BuildPoolPayloadJson, ref sLastPoolPayloadJson, true);
            return result;
        }

        internal static void RestorePoolMonitorPreferences()
        {
            PoolDebugger.EnableTracking = EditorPrefs.GetBool(POOL_TRACKING_PREF_KEY, PoolDebugger.EnableTracking);
            PoolDebugger.EnableEventHistory = EditorPrefs.GetBool(POOL_EVENT_HISTORY_PREF_KEY, PoolDebugger.EnableEventHistory);
            PoolDebugger.EnableStackTrace = EditorPrefs.GetBool(POOL_STACK_TRACE_PREF_KEY, PoolDebugger.EnableStackTrace);
            if (PoolDebugger.EnableStackTrace)
            {
                PoolDebugger.EnableTracking = true;
                PoolDebugger.EnableEventHistory = true;
            }
        }

        private static void SavePoolMonitorPreferences()
        {
            EditorPrefs.SetBool(POOL_TRACKING_PREF_KEY, PoolDebugger.EnableTracking);
            EditorPrefs.SetBool(POOL_EVENT_HISTORY_PREF_KEY, PoolDebugger.EnableEventHistory);
            EditorPrefs.SetBool(POOL_STACK_TRACE_PREF_KEY, PoolDebugger.EnableStackTrace);
        }

        private static string BuildResPayloadJson()
        {
            return sResHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildSingletonPayloadJson()
        {
            return sSingletonHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildAudioPayloadJson()
        {
            return sAudioHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildLogPayloadJson()
        {
            return LogKitCommandHandler.BuildSnapshotState();
        }

        private static string BuildSavePayloadJson()
        {
            if (!EnsureSaveHandler())
                return "{}";

            return sSaveHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildLocalizationPayloadJson()
        {
            if (!EnsureLocalizationHandler())
                return "{}";

            return sLocalizationHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildScenePayloadJson()
        {
            if (!EnsureSceneHandler())
                return "{}";

            return sSceneHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildSpatialPayloadJson()
        {
            if (!EnsureSpatialHandler())
                return "{}";

            return sSpatialHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildInputPayloadJson()
        {
            if (!EnsureInputHandler())
                return "{}";

            return sInputHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildUIKitPayloadJson()
        {
            if (!EnsureUIKitHandler())
                return "{}";

            return sUIKitHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildActionPayloadJson()
        {
            if (!EnsureActionHandler())
                return "{}";

            return sActionHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string GetDefaultYokiframeRoot()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return string.IsNullOrEmpty(projectRoot) ? null : Path.Combine(projectRoot, ".yokiframe");
        }
    }
}
#endif
#endif
