#if GODOT && TOOLS
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Windows Godot Runtime 的 Shared Memory Telemetry 写入门面：只保留平台门控、Try 错误策略和初始化编排，
    /// 帧写入协议全部委托给跨宿主共享的 <see cref="YokiFrameSharedMemoryTelemetryFrameWriter"/>。
    /// </summary>
    internal sealed class GodotSharedMemoryTelemetryWriter : IDisposable
    {
        private readonly YokiFrameSharedMemoryTelemetryFrameWriter mWriter;

        /// <summary>
        /// 获取当前 Godot Tools Host 是否已经建立项目级 telemetry 变化通知。
        /// </summary>
        public bool IsNotificationReady
        {
            get { return mWriter.IsNotificationReady; }
        }

        /// <summary>
        /// 为指定 Godot Runtime engine 创建 writer，并立即锁定跨宿主一致的稳定 engineIdHash。
        /// </summary>
        /// <param name="projectRoot">当前 Godot 项目绝对根目录。</param>
        /// <param name="engineId">当前 Host 的安全 engine 标识。</param>
        public GodotSharedMemoryTelemetryWriter(string projectRoot, string engineId)
        {
            // Godot 宿主把写入失败转换为 Try 结果，因此共享层诊断回调保持静默。
            mWriter = new YokiFrameSharedMemoryTelemetryFrameWriter(projectRoot, engineId);
        }

        /// <summary>
        /// 预创建首批 Kit 的 state memory map；当前 Client 不支持 named map 的平台会明确返回不可用。
        /// </summary>
        /// <param name="kits">需要发布 state 的安全 Kit 标识。</param>
        /// <param name="errorMessage">初始化失败时返回可诊断原因。</param>
        /// <returns>全部 map 可用时返回 true。</returns>
        public bool TryInitialize(string[] kits, out string errorMessage)
        {
            if (!OperatingSystem.IsWindows())
            {
                errorMessage = "Shared Memory Telemetry named maps are only enabled on Windows.";
                return false;
            }

            if (kits == null || kits.Length == 0)
            {
                errorMessage = "Telemetry state kits are required.";
                return false;
            }

            mWriter.Dispose();
            try
            {
                for (var index = 0; index < kits.Length; index++)
                {
                    mWriter.EnsureMap(kits[index], "state");
                }

                mWriter.OpenNotification();

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                mWriter.Dispose();
                errorMessage = "Shared Memory Telemetry initialization failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 按 Writing、header/payload、Committed 顺序写入一份完整最新状态帧，供 Client 双读 header 后安全消费。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="payloadJson">与 FileBridge snapshot 完全相同的 JSON payload。</param>
        /// <param name="generation">当前 Host generation。</param>
        /// <param name="sequence">当前状态序号。</param>
        /// <param name="errorMessage">写入失败时返回可诊断原因。</param>
        /// <returns>帧已提交时返回 true。</returns>
        public bool TryWriteState(
            string kit,
            string payloadJson,
            long generation,
            long sequence,
            out string errorMessage)
        {
            return TryWriteState(kit, "state", payloadJson, generation, sequence, out errorMessage);
        }

        /// <summary>
        /// 写入指定 Kit/name 的 Shared Memory latest frame；命名 frame 按需建立映射。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="name">Provider 声明的安全名称。</param>
        /// <param name="payloadJson">Kit 自有 schema JSON。</param>
        /// <param name="generation">当前 Host generation。</param>
        /// <param name="sequence">当前状态序号。</param>
        /// <param name="errorMessage">写入失败时返回可诊断原因。</param>
        /// <returns>帧已提交时返回 true。</returns>
        public bool TryWriteState(
            string kit,
            string name,
            string payloadJson,
            long generation,
            long sequence,
            out string errorMessage)
        {
            if (!OperatingSystem.IsWindows())
            {
                errorMessage = "Shared Memory Telemetry named maps are only enabled on Windows.";
                return false;
            }

            if (payloadJson == null)
            {
                errorMessage = "Telemetry payload is required.";
                return false;
            }

            try
            {
                mWriter.WriteFrame(kit, name, payloadJson, generation, sequence);
                errorMessage = string.Empty;
                return true;
            }
            catch (InvalidOperationException)
            {
                // 共享层对超限 payload 抛出固定异常；此处翻译为宿主稳定的失败语义。
                errorMessage = "Telemetry payload exceeds max payload bytes.";
                return false;
            }
            catch (Exception exception)
            {
                errorMessage = "Shared Memory Telemetry write failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>释放指定 Kit 已不再活动的命名 frame，标准 state frame 始终保留。</summary>
        /// <param name="kit">命名 frame 所属 Kit。</param>
        /// <param name="activeNames">本轮仍活动的安全名称。</param>
        public void RetainNamedStates(string kit, IReadOnlyList<string> activeNames)
        {
            mWriter.RetainNamedStates(kit, activeNames);
        }

        /// <summary>
        /// 释放本 Host 当前持有的所有 named map 与 accessor；停止后不应保留可误读的旧 generation。
        /// </summary>
        public void Dispose()
        {
            mWriter.Dispose();
        }
    }
}
#endif
