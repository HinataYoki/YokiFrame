#if !GODOT
using System;
using System.IO;
using System.Text;

namespace YokiFrame.Unity
{
    internal static partial class UnityCommandBridgeHost
    {
        internal static YokiCommandBridgeCore CreateEngineCore(string yokiframeRoot, KitCommandDispatcher dispatcher)
        {
            try
            {
                return new YokiCommandBridgeCore(GetEngineRoot(yokiframeRoot), dispatcher, new YokiCommandBridgeOptions
                {
                    EngineId = ENGINE_ID,
                    ProcessingTimeout = TimeSpan.FromSeconds(8)
                });
            }
            catch (Exception e)
            {
                LogKit.Warning($"[YokiCommandBridge] engine-scoped core 初始化失败: {e.Message}");
                return default;
            }
        }

        internal static string GetEngineRoot(string yokiframeRoot) =>
            Path.Combine(yokiframeRoot, "engines", ENGINE_ID);

        internal static string BuildBridgeStatusJson(YokiCommandBridgeCore engineCore)
        {
            var engineStatus = engineCore != default ? engineCore.BuildStatusJson() : BRIDGE_UNAVAILABLE_JSON;

            if (!IsJsonObject(engineStatus))
                engineStatus = BRIDGE_UNAVAILABLE_JSON;

            // 顶层保持原 core 状态字段，同时附带 engineScoped，供旧 UI 读取不丢诊断。
            var sb = new StringBuilder(engineStatus.Length * 2 + 32);
            sb.Append(engineStatus, 0, engineStatus.Length - 1);
            sb.Append(",\"engineScoped\":");
            sb.Append(engineStatus);
            sb.Append('}');
            return sb.ToString();
        }

        private static bool IsJsonObject(string json) =>
            !string.IsNullOrEmpty(json) && json[0] == '{' && json[json.Length - 1] == '}';

        private static void PollCores()
        {
            sEngineCore?.Poll();
        }
    }
}
#endif
