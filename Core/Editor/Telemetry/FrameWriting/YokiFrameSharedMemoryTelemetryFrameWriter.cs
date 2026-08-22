#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
// 跨宿主 Shared Memory Telemetry 帧写入协议的唯一实现：
// Unity 与 Godot 宿主只保留平台门控和错误策略，帧布局、双屏障提交顺序、named map 生命周期
// 与变化通知全部收敛在本类型，避免同一协议在两个 Adapter 中漂移。

using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Shared Memory Telemetry v1 的帧写入协议：按 Kit/name 管理 named map 生命周期，
    /// 以 Writing → header/payload → Committed 双内存屏障顺序提交最新帧，并在提交后触发项目级变化通知。
    /// 平台门控由宿主 Adapter 负责；payload 超限抛出 <see cref="InvalidOperationException"/>，IO 异常原样上抛，
    /// 由宿主决定转换为异常路径还是 Try 结果。
    /// </summary>
    public sealed class YokiFrameSharedMemoryTelemetryFrameWriter : IDisposable
    {
        private static readonly UTF8Encoding sUtf8 = new UTF8Encoding(false);

        private readonly string mEngineId;
        private readonly ulong mEngineIdHash;
        private readonly string mProjectScopeId;
        // 通知创建或触发失败时的一次性诊断回调；null 表示调用方不需要告警文本。
        private readonly Action<string> mWarn;
        private readonly Dictionary<string, MemoryMappedFile> mStateMaps = new Dictionary<string, MemoryMappedFile>(StringComparer.Ordinal);
        private readonly Dictionary<string, MemoryMappedViewAccessor> mStateAccessors = new Dictionary<string, MemoryMappedViewAccessor>(StringComparer.Ordinal);
        private EventWaitHandle mTelemetryNotification;
        private bool mWarned;

        /// <summary>
        /// 获取当前项目级 telemetry 变化通知是否已经建立。
        /// </summary>
        public bool IsNotificationReady
        {
            get { return mTelemetryNotification != null; }
        }

        /// <summary>
        /// 为指定项目与 engine 创建帧写入器，并锁定跨宿主一致的稳定 engineIdHash。
        /// </summary>
        /// <param name="projectRoot">当前宿主项目绝对根目录。</param>
        /// <param name="engineId">当前 Host 的安全 engine 标识。</param>
        /// <param name="warn">通知失败时的一次性诊断回调；传 null 表示静默。</param>
        public YokiFrameSharedMemoryTelemetryFrameWriter(
            string projectRoot,
            string engineId,
            Action<string> warn = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            if (string.IsNullOrWhiteSpace(engineId))
            {
                throw new ArgumentException("Engine id is required.", nameof(engineId));
            }

            mEngineId = engineId;
            mEngineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
            mProjectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
            mWarn = warn;
        }

        /// <summary>
        /// 创建当前项目的自动复位通知事件；失败时不影响 Shared Memory 本身，只经诊断回调报告一次。
        /// </summary>
        public void OpenNotification()
        {
            if (mTelemetryNotification != null)
            {
                return;
            }

            try
            {
                var name = YokiFrameSharedMemoryTelemetryNotificationName.Create(mProjectScopeId, mEngineId);
                mTelemetryNotification = new EventWaitHandle(false, EventResetMode.AutoReset, name, out _);
            }
            catch (Exception exception)
            {
                mTelemetryNotification = null;
                WarnOnce("YokiFrame telemetry notification unavailable: " + exception.Message);
            }
        }

        /// <summary>
        /// 预创建指定 Kit/name 的 named map；供宿主初始化阶段提前建立首批标准 state 段。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="name">Provider 声明的安全 telemetry 名称。</param>
        public void EnsureMap(string kit, string name)
        {
            EnsureStateAccessor(kit, name);
        }

        /// <summary>
        /// 按 Writing、header/payload、Committed 顺序写入一份完整最新状态帧，并在提交后触发变化通知。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="name">Provider 声明的安全 telemetry 名称。</param>
        /// <param name="payloadJson">与 FileBridge snapshot 完全相同的 JSON payload。</param>
        /// <param name="generation">当前 Host generation。</param>
        /// <param name="sequence">当前状态序号。</param>
        public void WriteFrame(
            string kit,
            string name,
            string payloadJson,
            long generation,
            long sequence)
        {
            if (payloadJson == null)
            {
                throw new ArgumentNullException(nameof(payloadJson));
            }

            var payloadBytes = sUtf8.GetBytes(payloadJson);
            if (payloadBytes.Length > YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
            {
                throw new InvalidOperationException("Telemetry payload exceeds max payload bytes.");
            }

            var accessor = EnsureStateAccessor(kit, name);
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
        }

        /// <summary>
        /// 释放指定 Kit 已不再活动的命名 frame 映射；标准 state frame 始终保留。
        /// </summary>
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
        /// 释放当前持有的全部 named map、accessor 与通知句柄；停止后不应保留可误读的旧 generation。
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
        /// 打开指定 Kit/name 的固定容量 named map 并缓存 accessor；打开或缓存失败时回滚已建资源。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <param name="name">安全 telemetry 名称。</param>
        /// <returns>已打开的可读写 view accessor。</returns>
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

        /// <summary>基于 Runtime contract 创建当前 engine/Kit 的标准 segment 名称。</summary>
        private string CreateSegmentName(string kit, string name)
        {
            return YokiFrameSharedMemoryTelemetrySegmentName.Create(mProjectScopeId, mEngineId, kit, name);
        }

        /// <summary>
        /// 写入除 writeState 外的全部可变 header 字段；writeState 状态位由调用方以内存屏障独立发布。
        /// </summary>
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

        /// <summary>
        /// 在 committed frame 发布后唤醒 Workbench；触发失败时释放失效句柄并经诊断回调报告一次。
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
            catch (Exception exception)
            {
                mTelemetryNotification.Dispose();
                mTelemetryNotification = null;
                WarnOnce("YokiFrame telemetry notification failed: " + exception.Message);
            }
        }

        /// <summary>只在第一次失败时输出诊断，避免周期刷新刷屏。</summary>
        private void WarnOnce(string message)
        {
            if (mWarn == null || mWarned)
            {
                return;
            }

            mWarned = true;
            mWarn(message);
        }
    }
}
#endif
