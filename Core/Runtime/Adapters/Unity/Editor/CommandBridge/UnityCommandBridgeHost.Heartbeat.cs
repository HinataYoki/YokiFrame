#if !GODOT
using System;
using System.IO;
using UnityEngine;

namespace YokiFrame.Unity
{
    internal static partial class UnityCommandBridgeHost
    {
        private static void WriteHeartbeat()
        {
            if (string.IsNullOrEmpty(sYokiframeRoot))
                return;

            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                WriteHeartbeatFile(sYokiframeRoot, timestamp);
            }
            catch (Exception e)
            {
                LogKit.Warning($"[YokiCommandBridge] 写入心跳失败: {e.Message}");
            }
        }

        private static void WriteEngineRegistry()
        {
            if (string.IsNullOrEmpty(sYokiframeRoot))
                return;

            try
            {
                WriteEngineRegistryFile(
                    sYokiframeRoot,
                    Application.unityVersion,
                    Path.GetDirectoryName(Application.dataPath));
            }
            catch (Exception e)
            {
                LogKit.Warning($"[YokiCommandBridge] 写入 engine registry 失败: {e.Message}");
            }
        }

        internal static string BuildHeartbeatJson(long timestamp) =>
            $"{{\"protocolVersion\":2,\"engineId\":\"{ENGINE_ID}\",\"timestamp\":{timestamp},\"createdAtUtc\":\"{DateTime.UtcNow:O}\"}}";

        internal static string BuildEngineRegistryJson(string unityVersion, string projectPath, string startedAtUtc)
        {
#if YOKIFRAME_LUBAN_SUPPORT
            const string LUBAN_AVAILABLE = "true";
#else
            const string LUBAN_AVAILABLE = "false";
#endif
            var implementedKitsJson = CommandBridgeKitRegistry.BuildImplementedKitsJson(CommandBridgeEngineKind.Unity);
            var kitFeaturesJson = CommandBridgeKitRegistry.BuildKitFeaturesJson(CommandBridgeEngineKind.Unity);
            return "{\"protocolVersion\":2,\"engineId\":\"" + ENGINE_ID +
                   "\",\"engine\":\"Unity\",\"version\":\"" + JsonHelper.EscapeString(unityVersion) +
                   "\",\"projectPath\":\"" + JsonHelper.EscapeString(projectPath) +
                   "\",\"adapterVersion\":\"2.0.0\",\"startedAtUtc\":\"" + JsonHelper.EscapeString(startedAtUtc) +
                   "\",\"capabilities\":[\"commands\",\"events\",\"heartbeat\",\"bridge_status\",\"snapshots\",\"telemetry\"]" +
                   ",\"implementedKits\":" + implementedKitsJson +
                   ",\"kitFeatures\":" + kitFeaturesJson +
                   ",\"optionalDependencies\":{\"luban\":{\"available\":" + LUBAN_AVAILABLE +
                   ",\"define\":\"YOKIFRAME_LUBAN_SUPPORT\"" +
                   ",\"packageName\":\"com.code-philosophy.luban\"" +
                   ",\"asmdefName\":\"Luban.Runtime\"" +
                   ",\"typeName\":\"Luban.ByteBuf\"}}}";
        }

        internal static void WriteHeartbeatFile(string yokiframeRoot, long timestamp)
        {
            var engineRoot = Path.Combine(yokiframeRoot, "engines", ENGINE_ID);
            var engineHeartbeatPath = Path.Combine(engineRoot, "status", "heartbeat.json");
            FileBridgeFileSystem.AtomicWriteAllTextInRoot(yokiframeRoot, engineHeartbeatPath, BuildHeartbeatJson(timestamp));
        }

        internal static void WriteEngineRegistryFile(string yokiframeRoot, string unityVersion, string projectPath)
        {
            var engineRoot = Path.Combine(yokiframeRoot, "engines", ENGINE_ID);
            var enginePath = Path.Combine(engineRoot, "engine.json");
            FileBridgeFileSystem.AtomicWriteAllTextInRoot(
                yokiframeRoot,
                enginePath,
                BuildEngineRegistryJson(unityVersion, projectPath, sStartedAtUtc));
        }
    }
}
#endif
