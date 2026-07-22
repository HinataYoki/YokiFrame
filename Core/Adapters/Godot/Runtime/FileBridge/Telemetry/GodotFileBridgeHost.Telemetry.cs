#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot FileBridge Host 的可选 Shared Memory Telemetry 初始化、发布、capability 和释放逻辑。
    /// </summary>
    public sealed partial class GodotFileBridgeHost
    {
        private static readonly string[] sFileBridgeCapabilities =
        {
            "snapshot.read",
            "command.send",
            "bridge.status"
        };

        private static readonly string[] sTelemetryCapabilities =
        {
            "snapshot.read",
            "command.send",
            "bridge.status",
            "telemetry.read"
        };

        private GodotSharedMemoryTelemetryWriter mTelemetryWriter;
        private bool mTelemetryAvailable;

        /// <summary>
        /// 在 Windows 上预创建首批 Kit 的 named map；当前平台不支持或初始化失败时保持 FileBridge-only。
        /// </summary>
        private void InitializeTelemetry()
        {
            DisposeTelemetry();
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var writer = new GodotSharedMemoryTelemetryWriter(mPaths.ProjectRoot, ENGINE_ID);
            if (!writer.TryInitialize(mStateKits, out var errorMessage))
            {
                writer.Dispose();
                mLastError = errorMessage;
                return;
            }

            mTelemetryWriter = writer;
            mTelemetryAvailable = true;
        }

        /// <summary>
        /// 尝试把已原子落盘的 snapshot payload 同步写入实时段；失败时立即撤销 capability 并保留 FileBridge 回退。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="payloadJson">与 snapshot 完全相同的 JSON payload。</param>
        private void PublishTelemetryState(string kit, string payloadJson)
        {
            PublishTelemetryState(kit, "state", payloadJson);
        }

        /// <summary>
        /// 尝试写入指定 Kit/name 的 latest frame；失败时撤销 capability 并保留 FileBridge 回退。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="name">Provider 声明的安全 Telemetry 名称。</param>
        /// <param name="payloadJson">Kit 自有 schema JSON。</param>
        private void PublishTelemetryState(string kit, string name, string payloadJson)
        {
            var writer = mTelemetryWriter;
            if (!mTelemetryAvailable || writer == null)
            {
                return;
            }

            if (writer.TryWriteState(kit, name, payloadJson, mGeneration, mSequence, out var errorMessage))
            {
                return;
            }

            mLastError = errorMessage;
            DisposeTelemetry();
        }

        /// <summary>
        /// 返回当前 Host 实际可提供的 capability；只有 writer 初始化并保持有效时才允许工具读取 telemetry。
        /// </summary>
        /// <returns>可安全发布到 engine registry 的 capability 列表。</returns>
        private string[] GetCapabilities()
        {
            if (!mTelemetryAvailable)
            {
                return sFileBridgeCapabilities;
            }

            if (mTelemetryWriter != null && mTelemetryWriter.IsNotificationReady)
            {
                return new[]
                {
                    "snapshot.read",
                    "command.send",
                    "bridge.status",
                    "telemetry.read",
                    "telemetry.notify"
                };
            }

            return sTelemetryCapabilities;
        }

        /// <summary>
        /// 关闭当前 writer 并清除 telemetry capability，防止 registry 宣称已经释放的实时段仍可使用。
        /// </summary>
        private void DisposeTelemetry()
        {
            var writer = mTelemetryWriter;
            mTelemetryWriter = null;
            mTelemetryAvailable = false;
            ClearNamedTelemetryVersions();
            writer?.Dispose();
        }
    }
}
#endif
