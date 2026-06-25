#if !GODOT
#if UNITY_EDITOR
using UnityEditor;
using YokiFrame;

namespace YokiFrame.Unity
{
    internal static class UnitySharedMemoryTelemetry
    {
        public const int DEFAULT_PAYLOAD_CAPACITY = AdapterSharedMemoryTelemetry.DEFAULT_PAYLOAD_CAPACITY;

        public static int DefaultPayloadCapacity => DEFAULT_PAYLOAD_CAPACITY;

        static UnitySharedMemoryTelemetry()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ResetForTests;
            AssemblyReloadEvents.beforeAssemblyReload += ResetForTests;
            EditorApplication.quitting -= ResetForTests;
            EditorApplication.quitting += ResetForTests;
        }

        public static string ChannelName(string engineId, string kit, string name)
        {
            return AdapterSharedMemoryTelemetry.ChannelName(engineId, kit, name);
        }

        public static bool TryWriteLatest(string engineId, string kit, string name, string payloadJson)
        {
            return AdapterSharedMemoryTelemetry.TryWriteLatest(engineId, kit, name, payloadJson);
        }

        internal static void WriteLatest(string engineId, string kit, string name, string payloadJson, int payloadCapacity)
        {
            AdapterSharedMemoryTelemetry.WriteLatest(engineId, kit, name, payloadJson, payloadCapacity);
        }

        internal static string PosixChannelName(string logicalChannelName)
        {
            return AdapterSharedMemoryTelemetry.PosixChannelName(logicalChannelName);
        }

        internal static void ResetForTests()
        {
            AdapterSharedMemoryTelemetry.ResetForTests();
        }
    }
}
#endif
#endif
