#if GODOT
using System;
using System.IO;
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 命令桥驱动壳：负责初始化并轮询 YokiCommandBridgeCore。
    /// </summary>
    public sealed class GodotCommandBridgeHost
    {
        private const string ENGINE_ID = "godot-runtime";
        private const int POLL_MIN_INTERVAL_MS = 100;
        private const int POLL_MAX_INTERVAL_MS = 1000;
        private const int HEARTBEAT_INTERVAL_MS = 2000;
        private const string CODE_EDITOR_PATH_SETTING_KEY = "yokiframe/code_editor/path";
        private const string CODE_EDITOR_ARGUMENTS_SETTING_KEY = "yokiframe/code_editor/arguments";
        private const string CODE_EDITOR_PATH_ENVIRONMENT_KEY = "YOKIFRAME_CODE_EDITOR_PATH";
        private const string CODE_EDITOR_ARGUMENTS_ENVIRONMENT_KEY = "YOKIFRAME_CODE_EDITOR_ARGS";
        private const string DEFAULT_CODE_EDITOR_ARGUMENTS = "{file}";
        private const string SAVE_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SaveKitCommandHandler, YokiFrame.SaveKit";
        private const string LOCALIZATION_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.LocalizationKitCommandHandler, YokiFrame.LocalizationKit";
        private const string SCENE_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SceneKitCommandHandler, YokiFrame.SceneKit";
        private const string SPATIAL_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SpatialKitCommandHandler, YokiFrame.SpatialKit";
        private const string INPUT_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.InputKitCommandHandler, YokiFrame.InputKit";
        private const string ACTION_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.ActionKitCommandHandler, YokiFrame.ActionKit";

        private readonly KitCommandDispatcher mDispatcher = new KitCommandDispatcher();
        private YokiCommandBridgeCore mCore;
        private string mBridgeRootPath;
        private string mStartedAtUtc;
        private readonly CommandBridgePollBackoff mPollBackoff =
            new CommandBridgePollBackoff(POLL_MIN_INTERVAL_MS, POLL_MAX_INTERVAL_MS);
        private double mPollAccumulatorMs;
        private double mHeartbeatAccumulatorMs;

        public void EnsureInitialized()
        {
            if (mCore != null)
                return;

            mBridgeRootPath = ResolveBridgeRootPath();
            mStartedAtUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(mBridgeRootPath);
            GodotEventStreamWriter.Init(mBridgeRootPath);
            GodotFsmKitEventBridge.Init(mBridgeRootPath);
            GodotEventKitEventBridge.Init(mBridgeRootPath);
            GodotKitStateSnapshotPublisher.Init(mBridgeRootPath);

            mDispatcher.DefaultEngineId = ENGINE_ID;
            mDispatcher.Register(new SystemCommandHandler(
                () => mCore != null
                    ? mCore.BuildStatusJson()
                    : "{\"available\":false,\"reason\":\"bridge is not initialized\"}",
                OpenCodeLocationWithGodotShell,
                () => mDispatcher.BuildCommandCatalogJson(),
                () => mCore != null
                    ? mCore.BuildStatusDetailJson()
                    : "{\"available\":false,\"reason\":\"bridge is not initialized\"}"));
            mDispatcher.Register(new FsmKitCommandHandler());
            mDispatcher.Register(new PoolKitCommandHandler());
            mDispatcher.Register(new LogKitCommandHandler());
            mDispatcher.Register(new ResKitCommandHandler());
            mDispatcher.Register(new EventKitCommandHandler());
            mDispatcher.Register(new SingletonKitCommandHandler());
            mDispatcher.Register(new ArchitectureCommandHandler());
            mDispatcher.Register(new AudioKitCommandHandler());
            RegisterOptionalToolCommandHandlers();
            mCore = new YokiCommandBridgeCore(Path.Combine(mBridgeRootPath, "engines", ENGINE_ID), mDispatcher,
                new YokiCommandBridgeOptions
                {
                    EngineId = ENGINE_ID,
                    ProcessingTimeout = TimeSpan.FromSeconds(8)
                });

            WriteEngineFiles();
        }

        private void RegisterOptionalToolCommandHandlers()
        {
            // Tools Kit 可以独立安装，命令桥只按类型名尝试挂载，避免 Godot Adapter 反向硬依赖每个 Kit。
            OptionalKitCommandHandlerRegistry.TryRegister(mDispatcher, SAVE_KIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(mDispatcher, LOCALIZATION_KIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(mDispatcher, SCENE_KIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(mDispatcher, SPATIAL_KIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(mDispatcher, INPUT_KIT_COMMAND_HANDLER_TYPE);
            OptionalKitCommandHandlerRegistry.TryRegister(mDispatcher, ACTION_KIT_COMMAND_HANDLER_TYPE);
        }

        private static string ResolveBridgeRootPath()
        {
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            return Path.Combine(projectRoot, ".yokiframe");
        }

        public void Tick(double delta)
        {
            if (mCore == null)
                return;

            GodotFsmKitEventBridge.Tick(delta);
            GodotEventKitEventBridge.Tick(delta);
            GodotKitStateSnapshotPublisher.Tick(delta);

            mPollAccumulatorMs += delta * 1000.0;
            if (mPollAccumulatorMs >= mPollBackoff.CurrentIntervalMs)
            {
                mPollAccumulatorMs = 0.0;
                mCore.Poll();
                mPollBackoff.RecordPollResult(mCore.LastPollHadActivity || mCore.BackpressureActive);
            }

            mHeartbeatAccumulatorMs += delta * 1000.0;
            if (mHeartbeatAccumulatorMs >= HEARTBEAT_INTERVAL_MS)
            {
                mHeartbeatAccumulatorMs = 0.0;
                WriteEngineFiles();
            }
        }

        private void WriteEngineFiles()
        {
            if (string.IsNullOrEmpty(mBridgeRootPath))
                return;

            var projectRoot = ProjectSettings.GlobalizePath("res://");
            var engineRoot = Path.Combine(mBridgeRootPath, "engines", ENGINE_ID);
            var enginePath = Path.Combine(engineRoot, "engine.json");
            var heartbeatPath = Path.Combine(engineRoot, "status", "heartbeat.json");
            var heartbeatJson = BuildHeartbeatJson(GetUnixTimestampSeconds());

            FileBridgeFileSystem.AtomicWriteAllTextInRoot(mBridgeRootPath, enginePath,
                BuildEngineRegistryJson(GetGodotVersion(), projectRoot, mStartedAtUtc));
            FileBridgeFileSystem.AtomicWriteAllTextInRoot(mBridgeRootPath, heartbeatPath, heartbeatJson);
        }

        private static string BuildHeartbeatJson(long timestamp)
        {
            return "{\"protocolVersion\":2,\"engineId\":\"" + ENGINE_ID +
                   "\",\"timestamp\":" + timestamp +
                   ",\"createdAtUtc\":\"" + JsonHelper.EscapeString(DateTime.UtcNow.ToString("O")) + "\"}";
        }

        private static string BuildEngineRegistryJson(string version, string projectPath, string startedAtUtc)
        {
            var lubanAvailable = GodotDependencyDefineService.IsLubanEnvironmentAvailable() ? "true" : "false";
            var implementedKitsJson = CommandBridgeKitRegistry.BuildImplementedKitsJson(CommandBridgeEngineKind.Godot);
            var kitFeaturesJson = CommandBridgeKitRegistry.BuildKitFeaturesJson(CommandBridgeEngineKind.Godot);
            return "{\"protocolVersion\":2,\"engineId\":\"" + ENGINE_ID +
                   "\",\"engine\":\"Godot\",\"version\":\"" + JsonHelper.EscapeString(version) +
                   "\",\"projectPath\":\"" + JsonHelper.EscapeString(projectPath) +
                   "\",\"adapterVersion\":\"2.0.0\",\"startedAtUtc\":\"" + JsonHelper.EscapeString(startedAtUtc) +
                   "\",\"capabilities\":[\"commands\",\"heartbeat\",\"bridge_status\",\"events\",\"snapshots\",\"telemetry\"]" +
                   ",\"implementedKits\":" + implementedKitsJson +
                   ",\"kitFeatures\":" + kitFeaturesJson +
                   ",\"optionalDependencies\":{\"luban\":{\"available\":" + lubanAvailable +
                   ",\"define\":\"YOKIFRAME_LUBAN_SUPPORT\"" +
                   ",\"packageName\":\"com.code-philosophy.luban\"" +
                   ",\"asmdefName\":\"Luban.Runtime\"" +
                   ",\"typeName\":\"Luban.ByteBuf\"}}}";
        }

        private static string GetGodotVersion()
        {
            try
            {
                var versionInfo = Engine.GetVersionInfo();
                if (versionInfo.TryGetValue("string", out var version))
                    return version.ToString();
            }
            catch
            {
                // 版本信息只用于诊断展示，读取失败时保持桥正常启动。
            }

            return string.Empty;
        }

        internal static string OpenCodeLocationWithGodotShell(string filePath, int line)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath is empty");

            var fullPath = ResolveProjectFilePath(filePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("code location file does not exist", fullPath);

            var clampedLine = line > 0 ? line : 1;
            string editorName;
            int processId;
            bool linePositionSupported;
            var openedByConfiguredEditor = TryOpenCodeLocationWithConfiguredEditor(fullPath, clampedLine, out editorName, out processId, out linePositionSupported);
            if (!openedByConfiguredEditor)
            {
                var error = OS.ShellOpen(fullPath);
                if (error != Error.Ok)
                    throw new InvalidOperationException("Godot OS.ShellOpen failed: " + error);

                editorName = "godot-shell-open";
                linePositionSupported = false;
            }

            return "{\"opened\":true,\"editor\":\"" + JsonHelper.EscapeString(editorName) + "\",\"filePath\":\"" +
                   JsonHelper.EscapeString(fullPath.Replace('\\', '/')) +
                   "\",\"line\":" + clampedLine +
                   ",\"linePositionSupported\":" + (linePositionSupported ? "true" : "false") +
                   ",\"processId\":" + processId +
                   "}";
        }

        private static bool TryOpenCodeLocationWithConfiguredEditor(string fullPath, int line, out string editorName, out int processId, out bool linePositionSupported)
        {
            editorName = string.Empty;
            processId = -1;
            linePositionSupported = false;

            var editorPath = ResolveConfiguredCodeEditorPath();
            if (string.IsNullOrEmpty(editorPath))
                return false;

            var argumentsTemplate = ResolveConfiguredCodeEditorArguments();
            linePositionSupported = argumentsTemplate.IndexOf("{line}", StringComparison.Ordinal) >= 0;
            var arguments = BuildCodeEditorArguments(argumentsTemplate, fullPath, line);
            processId = OS.CreateProcess(editorPath, arguments, false);
            if (processId <= 0)
                return false;

            editorName = "godot-configured-editor";
            return true;
        }

        private static string ResolveConfiguredCodeEditorPath()
        {
            var configuredPath = ReadGodotStringSetting(CODE_EDITOR_PATH_SETTING_KEY);
            if (string.IsNullOrEmpty(configuredPath))
                configuredPath = ReadGodotEnvironment(CODE_EDITOR_PATH_ENVIRONMENT_KEY);

            return configuredPath.Trim();
        }

        private static string ResolveConfiguredCodeEditorArguments()
        {
            var arguments = ReadGodotStringSetting(CODE_EDITOR_ARGUMENTS_SETTING_KEY);
            if (string.IsNullOrEmpty(arguments))
                arguments = ReadGodotEnvironment(CODE_EDITOR_ARGUMENTS_ENVIRONMENT_KEY);

            return string.IsNullOrEmpty(arguments) ? DEFAULT_CODE_EDITOR_ARGUMENTS : arguments;
        }

        private static string[] BuildCodeEditorArguments(string argumentsTemplate, string fullPath, int line)
        {
            var tokens = TokenizeCodeEditorArguments(argumentsTemplate);
            if (tokens.Count == 0)
                tokens.Add(DEFAULT_CODE_EDITOR_ARGUMENTS);

            var arguments = new string[tokens.Count];
            var projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://")).Replace('\\', '/').TrimEnd('/');
            var normalizedPath = fullPath.Replace('\\', '/');
            var lineText = line.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (var i = 0; i < tokens.Count; i++)
            {
                arguments[i] = tokens[i]
                    .Replace("{file}", normalizedPath)
                    .Replace("{line}", lineText)
                    .Replace("{column}", "1")
                    .Replace("{project}", projectRoot);
            }

            return arguments;
        }

        private static System.Collections.Generic.List<string> TokenizeCodeEditorArguments(string argumentsTemplate)
        {
            var tokens = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(argumentsTemplate))
                return tokens;

            var current = new System.Text.StringBuilder(argumentsTemplate.Length);
            var inQuotes = false;
            for (var i = 0; i < argumentsTemplate.Length; i++)
            {
                var c = argumentsTemplate[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    FlushCodeEditorArgumentToken(tokens, current);
                    continue;
                }

                current.Append(c);
            }

            FlushCodeEditorArgumentToken(tokens, current);
            return tokens;
        }

        private static void FlushCodeEditorArgumentToken(System.Collections.Generic.List<string> tokens, System.Text.StringBuilder current)
        {
            if (current.Length <= 0)
                return;

            tokens.Add(current.ToString());
            current.Length = 0;
        }

        private static string ReadGodotStringSetting(string key)
        {
            try
            {
                if (!ProjectSettings.HasSetting(key))
                    return string.Empty;

                return ProjectSettings.GetSetting(key).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadGodotEnvironment(string key)
        {
            try
            {
                return OS.HasEnvironment(key) ? OS.GetEnvironment(key) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveProjectFilePath(string filePath)
        {
            var projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            var normalizedPath = filePath.Replace('\\', '/');
            string fullPath;

            if (normalizedPath.StartsWith("res://", StringComparison.Ordinal))
            {
                fullPath = Path.GetFullPath(ProjectSettings.GlobalizePath(normalizedPath));
            }
            else if (Path.IsPathRooted(filePath))
            {
                fullPath = Path.GetFullPath(filePath);
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
            }

            var rootPath = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("code location is outside Godot project root");

            return fullPath;
        }

        private static long GetUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
#endif
