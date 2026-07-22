#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 在 Windows Godot Runtime 中为每个 Kit/state 写入 Shared Memory Telemetry v1 最新帧。
    /// </summary>
    internal sealed class GodotSharedMemoryTelemetryWriter : IDisposable
    {
        private static readonly UTF8Encoding sUtf8 = new UTF8Encoding(false);

        private readonly string mEngineId;
        private readonly ulong mEngineIdHash;
        private readonly string mProjectScopeId;
        private readonly Dictionary<string, MemoryMappedFile> mStateMaps = new Dictionary<string, MemoryMappedFile>(StringComparer.Ordinal);
        private readonly Dictionary<string, MemoryMappedViewAccessor> mStateAccessors = new Dictionary<string, MemoryMappedViewAccessor>(StringComparer.Ordinal);
        private EventWaitHandle mTelemetryNotification;

        /// <summary>
        /// 获取当前 Godot Tools Host 是否已经建立项目级 telemetry 变化通知。
        /// </summary>
        public bool IsNotificationReady
        {
            get { return mTelemetryNotification != null; }
        }

        /// <summary>
        /// 为指定 Godot Runtime engine 创建 writer，并立即锁定跨宿主一致的稳定 engineIdHash。
        /// </summary>
        /// <param name="projectRoot">当前 Godot 项目绝对根目录。</param>
        /// <param name="engineId">当前 Host 的安全 engine 标识。</param>
        public GodotSharedMemoryTelemetryWriter(string projectRoot, string engineId)
        {
            mEngineId = engineId;
            mEngineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
            mProjectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
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

            Dispose();
            try
            {
                for (var index = 0; index < kits.Length; index++)
                {
                    EnsureStateAccessor(kits[index], "state");
                }

                EnsureTelemetryNotification();

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
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
                var accessor = EnsureStateAccessor(kit, name);
                var payloadBytes = sUtf8.GetBytes(payloadJson);
                if (payloadBytes.Length > YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
                {
                    errorMessage = "Telemetry payload exceeds max payload bytes.";
                    return false;
                }

                var writtenTicks = DateTimeOffset.UtcNow.UtcTicks;
                var payloadCrc32 = YokiFrameSharedMemoryTelemetryCrc32.Compute(payloadBytes);
                accessor.Write(
                    YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET,
                    YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_WRITING);
                Thread.MemoryBarrier();
                WriteHeaderFields(accessor, generation, sequence, writtenTicks, payloadBytes.Length, payloadCrc32);
                accessor.WriteArray(
                    YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET,
                    payloadBytes,
                    0,
                    payloadBytes.Length);
                Thread.MemoryBarrier();
                accessor.Write(
                    YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET,
                    YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_COMMITTED);
                SignalTelemetryNotification();
                errorMessage = string.Empty;
                return true;
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
            HashSet<string> retainedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < activeNames.Count; index++)
            {
                retainedKeys.Add(CreateStateKey(kit, activeNames[index]));
            }

            string standardStateKey = CreateStateKey(kit, "state");
            List<string> staleKeys = new List<string>();
            foreach (var key in mStateAccessors.Keys)
            {
                if (key.StartsWith(kit + "\n", StringComparison.Ordinal)
                    && key != standardStateKey
                    && !retainedKeys.Contains(key))
                {
                    staleKeys.Add(key);
                }
            }

            for (var index = 0; index < staleKeys.Count; index++)
            {
                ReleaseState(staleKeys[index]);
            }
        }

        /// <summary>
        /// 释放本 Host 当前持有的所有 named map 与 accessor；停止后不应保留可误读的旧 generation。
        /// </summary>
        public void Dispose()
        {
            foreach (var accessor in mStateAccessors.Values)
            {
                accessor.Dispose();
            }

            foreach (var memoryMap in mStateMaps.Values)
            {
                memoryMap.Dispose();
            }

            mStateAccessors.Clear();
            mStateMaps.Clear();
            mTelemetryNotification?.Dispose();
            mTelemetryNotification = null;
        }

        /// <summary>
        /// 创建当前 Godot 项目的自动复位通知事件；通知不可用时仍保留 Shared Memory。
        /// </summary>
        private void EnsureTelemetryNotification()
        {
            if (!OperatingSystem.IsWindows() || mTelemetryNotification != null)
            {
                return;
            }

            try
            {
                string name = YokiFrameSharedMemoryTelemetryNotificationName.Create(
                    mProjectScopeId,
                    mEngineId);
                mTelemetryNotification = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    name,
                    out _);
            }
            catch (Exception)
            {
                mTelemetryNotification = null;
            }
        }

        /// <summary>
        /// 在 committed frame 发布后唤醒 Workbench；通知失败不改变已提交帧结果。
        /// </summary>
        private void SignalTelemetryNotification()
        {
            if (mTelemetryNotification == null)
            {
                return;
            }

            try
            {
                mTelemetryNotification.Set();
            }
            catch (Exception)
            {
                mTelemetryNotification.Dispose();
                mTelemetryNotification = null;
            }
        }

        /// <summary>
        /// 打开指定 Kit/state 的固定容量 named map，并缓存 accessor 供 Host 主线程复用。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <returns>已打开的可读写 accessor。</returns>
        [SupportedOSPlatform("windows")]
        private MemoryMappedViewAccessor EnsureStateAccessor(string kit, string name)
        {
            string stateKey = CreateStateKey(kit, name);
            if (mStateAccessors.TryGetValue(stateKey, out var existingAccessor))
            {
                return existingAccessor;
            }

            var capacity = YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET
                + YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES;
            var memoryMap = MemoryMappedFile.CreateOrOpen(
                CreateSegmentName(kit, name),
                capacity,
                MemoryMappedFileAccess.ReadWrite);
            try
            {
                var accessor = memoryMap.CreateViewAccessor(0, capacity, MemoryMappedFileAccess.ReadWrite);
                try
                {
                    mStateMaps.Add(stateKey, memoryMap);
                    mStateAccessors.Add(stateKey, accessor);
                    return accessor;
                }
                catch
                {
                    mStateMaps.Remove(stateKey);
                    accessor.Dispose();
                    throw;
                }
            }
            catch
            {
                memoryMap.Dispose();
                throw;
            }
        }

        /// <summary>创建不会与合法 SafeId 冲突的内部 Kit/name 字典键。</summary>
        private static string CreateStateKey(string kit, string name)
        {
            return kit + "\n" + name;
        }

        /// <summary>释放一个内部状态键对应的 accessor 与 memory map。</summary>
        private void ReleaseState(string stateKey)
        {
            if (mStateAccessors.TryGetValue(stateKey, out var accessor))
            {
                mStateAccessors.Remove(stateKey);
                accessor.Dispose();
            }

            if (mStateMaps.TryGetValue(stateKey, out var memoryMap))
            {
                mStateMaps.Remove(stateKey);
                memoryMap.Dispose();
            }
        }

        /// <summary>
        /// 基于 Runtime contract 创建当前 engine/Kit 的标准 state segment 名称。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <returns>跨进程 named map 名称。</returns>
        private string CreateSegmentName(string kit, string name)
        {
            return YokiFrameSharedMemoryTelemetrySegmentName.Create(mProjectScopeId, mEngineId, kit, name);
        }

        /// <summary>
        /// 写入除 writeState 外的可变 header 字段；调用方负责在内存屏障前后独立发布状态位。
        /// </summary>
        /// <param name="accessor">目标 named map accessor。</param>
        /// <param name="generation">当前 Host generation。</param>
        /// <param name="sequence">当前状态序号。</param>
        /// <param name="writtenTicks">本次写入 UTC ticks。</param>
        /// <param name="payloadLength">payload 字节长度。</param>
        /// <param name="payloadCrc32">payload IEEE CRC32。</param>
        private void WriteHeaderFields(
            MemoryMappedViewAccessor accessor,
            long generation,
            long sequence,
            long writtenTicks,
            int payloadLength,
            uint payloadCrc32)
        {
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET, YokiFrameSharedMemoryTelemetryContract.MAGIC);
            accessor.Write(
                YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET,
                YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET, mEngineIdHash);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET, generation);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET, sequence);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET, writtenTicks);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET, payloadLength);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET, payloadCrc32);
        }
    }
}
#endif
