#if !GODOT
using System;
using System.IO;

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
            return BuildBridgeStatusJson(engineCore, false);
        }

        internal static string BuildBridgeStatusDetailJson(YokiCommandBridgeCore engineCore)
        {
            return BuildBridgeStatusJson(engineCore, true);
        }

        private static string BuildBridgeStatusJson(YokiCommandBridgeCore engineCore, bool detail)
        {
            var engineStatus = engineCore != default
                ? (detail ? engineCore.BuildStatusDetailJson() : engineCore.BuildStatusJson())
                : BRIDGE_UNAVAILABLE_JSON;

            return IsJsonObject(engineStatus) ? engineStatus : BRIDGE_UNAVAILABLE_JSON;
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
