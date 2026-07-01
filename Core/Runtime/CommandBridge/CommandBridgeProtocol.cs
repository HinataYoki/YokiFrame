using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 文件桥协议标识符校验工具。
    /// </summary>
    public static class CommandBridgeProtocol
    {
        /// <summary>
        /// 获取 requestId、engineId、source、kit、action 等协议标识符的最大长度。
        /// </summary>
        public const int MAX_IDENTIFIER_LENGTH = 128;

        /// <summary>
        /// 判断字符串是否符合文件桥协议安全标识符规则。
        /// </summary>
        /// <param name="value">待校验的标识符。</param>
        /// <returns>符合规则返回 true，否则返回 false。</returns>
        public static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MAX_IDENTIFIER_LENGTH)
                return false;
            if (value == "." || value == "..")
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var isLetter = c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
                var isDigit = c >= '0' && c <= '9';
                if (isLetter || isDigit || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 支持 engine registry 的宿主类型。
    /// </summary>
    public enum CommandBridgeEngineKind
    {
        Unity,
        Godot
    }

    /// <summary>
    /// 命令桥 Kit 注册表 JSON 构建器。Unity/Godot 共用同一份 descriptor，避免 engine registry 手写分叉。
    /// </summary>
    public static class CommandBridgeKitRegistry
    {
        private const int UNITY_MASK = 1;
        private const int GODOT_MASK = 2;
        private const int BOTH_MASK = UNITY_MASK | GODOT_MASK;

        private static readonly string[] sSystemFeatures = { "commands", "bridge_status" };
        private static readonly string[] sRuntimeSnapshotTelemetryFeatures = { "runtime", "snapshots", "telemetry" };
        private static readonly string[] sEventKitUnityFeatures = { "runtime", "events", "snapshots", "telemetry", "static_scan" };
        private static readonly string[] sEventKitGodotFeatures = { "runtime", "events", "snapshots", "telemetry" };
        private static readonly string[] sFsmFeatures = { "runtime", "events", "snapshots", "telemetry" };
        private static readonly string[] sTableKitFeatures = { "tauri_config", "registry_optional_dependencies" };
        private static readonly string[] sUIKitFeatures = { "runtime", "snapshots", "telemetry", "ui_editor_tools" };
        private static readonly string[] sManagedRuntimeFeatures = { "runtime_selection", "workflow_actions", "build_pipeline", "backend_settings", "registry_optional_dependencies" };

        private static readonly KitDescriptor[] sDescriptors =
        {
            new KitDescriptor("System", BOTH_MASK, sSystemFeatures, null, null),
            new KitDescriptor("Architecture", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("EventKit", BOTH_MASK, null, sEventKitUnityFeatures, sEventKitGodotFeatures),
            new KitDescriptor("FsmKit", BOTH_MASK, sFsmFeatures, null, null),
            new KitDescriptor("LogKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("PoolKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("ResKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("SingletonKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("ManagedRuntimeKit", BOTH_MASK, sManagedRuntimeFeatures, null, null),
            new KitDescriptor("ActionKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("AudioKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("LocalizationKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("SaveKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("SceneKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("SpatialKit", BOTH_MASK, sRuntimeSnapshotTelemetryFeatures, null, null),
            new KitDescriptor("TableKit", BOTH_MASK, sTableKitFeatures, null, null),
            new KitDescriptor("UIKit", UNITY_MASK, sUIKitFeatures, null, null)
        };

        /// <summary>
        /// 构建指定宿主当前实现的 Kit 列表 JSON。
        /// </summary>
        public static string BuildImplementedKitsJson(CommandBridgeEngineKind engineKind)
        {
            var mask = GetMask(engineKind);
            var sb = new StringBuilder(256);
            sb.Append('[');
            var first = true;
            for (var i = 0; i < sDescriptors.Length; i++)
            {
                var descriptor = sDescriptors[i];
                if ((descriptor.EngineMask & mask) == 0)
                    continue;

                if (!first)
                    sb.Append(',');

                first = false;
                AppendQuoted(sb, descriptor.Name);
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// 构建指定宿主的 Kit 功能表 JSON。
        /// </summary>
        public static string BuildKitFeaturesJson(CommandBridgeEngineKind engineKind)
        {
            var mask = GetMask(engineKind);
            var sb = new StringBuilder(512);
            sb.Append('{');
            var firstKit = true;
            for (var i = 0; i < sDescriptors.Length; i++)
            {
                var descriptor = sDescriptors[i];
                if ((descriptor.EngineMask & mask) == 0)
                    continue;

                if (!firstKit)
                    sb.Append(',');

                firstKit = false;
                AppendQuoted(sb, descriptor.Name);
                sb.Append(':');
                AppendStringArray(sb, descriptor.GetFeatures(engineKind));
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static int GetMask(CommandBridgeEngineKind engineKind)
        {
            return engineKind == CommandBridgeEngineKind.Unity ? UNITY_MASK : GODOT_MASK;
        }

        private static void AppendStringArray(StringBuilder sb, string[] values)
        {
            sb.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    AppendQuoted(sb, values[i]);
                }
            }

            sb.Append(']');
        }

        private static void AppendQuoted(StringBuilder sb, string value)
        {
            sb.Append('"');
            sb.Append(JsonHelper.EscapeString(value));
            sb.Append('"');
        }

        private readonly struct KitDescriptor
        {
            public readonly string Name;
            public readonly int EngineMask;
            private readonly string[] mDefaultFeatures;
            private readonly string[] mUnityFeatures;
            private readonly string[] mGodotFeatures;

            public KitDescriptor(string name, int engineMask, string[] defaultFeatures, string[] unityFeatures, string[] godotFeatures)
            {
                Name = name;
                EngineMask = engineMask;
                mDefaultFeatures = defaultFeatures;
                mUnityFeatures = unityFeatures;
                mGodotFeatures = godotFeatures;
            }

            public string[] GetFeatures(CommandBridgeEngineKind engineKind)
            {
                if (engineKind == CommandBridgeEngineKind.Unity && mUnityFeatures != null)
                    return mUnityFeatures;

                if (engineKind == CommandBridgeEngineKind.Godot && mGodotFeatures != null)
                    return mGodotFeatures;

                return mDefaultFeatures ?? Array.Empty<string>();
            }
        }
    }
}
