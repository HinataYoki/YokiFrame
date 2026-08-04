using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Client.FastChannel;
using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Client.Transports.FileBridge;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client;

/// <summary>
/// 组合 FileBridge 与 Shared Memory telemetry，为工具应用层提供统一的本地客户端实现。
/// </summary>
public sealed partial class YokiFrameClient : IYokiFrameClient, IDisposable
{
    private const int MAX_CACHED_TELEMETRY_TARGETS = 128;
    private readonly FileBridgeTransport mFileBridgeTransport;
    private readonly FastChannelCommandTransport mFastChannelCommandTransport;
    private readonly object mTelemetryTargetsGate = new();
    private readonly Dictionary<TelemetryTargetKey, TelemetryReadTarget> mTelemetryTargets = new();
    private bool mDisposed;

    /// <summary>
    /// 使用项目根目录创建客户端；构造过程不会读写协议文件。
    /// </summary>
    /// <param name="projectRoot">包含 `.yokiframe` 的宿主项目根目录。</param>
    public YokiFrameClient(string projectRoot)
    {
        mFileBridgeTransport = new FileBridgeTransport(projectRoot);
        mFastChannelCommandTransport = new FastChannelCommandTransport(mFileBridgeTransport);
    }

    /// <summary>
    /// 获取当前项目的 YokiFrame 标准路径解析器。
    /// </summary>
    public YokiFramePaths Paths
    {
        get
        {
            ThrowIfDisposed();
            return mFileBridgeTransport.Paths;
        }
    }

    /// <summary>
    /// 读取 harness capability 文件。
    /// </summary>
    /// <returns>capability JSON 节点。</returns>
    public JsonNode ReadHarnessCapabilities()
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.ReadHarnessCapabilities();
    }

    /// <summary>
    /// 读取当前项目注册的全部 engine。
    /// </summary>
    /// <returns>engine registry 条目。</returns>
    public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries()
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.ReadEngineEntries();
    }

    /// <summary>
    /// 读取指定 Kit 的 snapshot。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <returns>snapshot JSON 节点。</returns>
    public JsonNode ReadSnapshot(string engineId, string kit, string name)
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.ReadSnapshot(engineId, kit, name);
    }

    /// <summary>
    /// 读取指定 engine 的 heartbeat。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>heartbeat；文件不存在时返回 null。</returns>
    public HeartbeatInfo? ReadHeartbeat(string engineId)
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.ReadHeartbeat(engineId);
    }

    /// <summary>
    /// 汇总指定 engine 的 FileBridge 状态。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>FileBridge 状态。</returns>
    public FileBridgeStatus ReadBridgeStatus(string engineId)
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.ReadBridgeStatus(engineId);
    }

    /// <summary>
    /// 按 requestId 查询可靠 FileBridge 请求当前所在状态和证据。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="requestId">请求标识。</param>
    /// <returns>请求状态快照。</returns>
    public CommandRequestStatus ReadCommandStatus(string engineId, string requestId)
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.ReadCommandStatus(engineId, requestId);
    }

    /// <summary>
    /// 从宿主发布的命名 Shared Memory segment 读取最新 telemetry 帧。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">状态名称。</param>
    /// <param name="expectedGeneration">期望 generation；为空时不校验。</param>
    /// <param name="maxPayloadBytes">允许的最大 payload 字节数。</param>
    /// <returns>telemetry 帧读取结果。</returns>
    public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        int maxPayloadBytes)
    {
        TelemetryReadTarget target;
        lock (mTelemetryTargetsGate)
        {
            ThrowIfDisposedUnderGate();
            target = GetTelemetryReadTarget(engineId, kit, name);
        }

        return target.Read(expectedGeneration, maxPayloadBytes);
    }

    /// <summary>
    /// 只读取晚于指定游标的 Shared Memory telemetry 帧；未变化时在 payload 复制前返回空。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">状态名称。</param>
    /// <param name="expectedGeneration">期望 generation；为空时不校验。</param>
    /// <param name="maxPayloadBytes">允许的最大 payload 字节数。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号。</param>
    /// <returns>新帧或读取失败结果；稳定未变化帧返回空。</returns>
    public SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        int maxPayloadBytes,
        long afterSequence)
    {
        TelemetryReadTarget target;
        lock (mTelemetryTargetsGate)
        {
            ThrowIfDisposedUnderGate();
            target = GetTelemetryReadTarget(engineId, kit, name);
        }

        return target.ReadIfChanged(expectedGeneration, maxPayloadBytes, afterSequence);
    }

    /// <summary>
    /// 尝试打开宿主发布的项目级 Shared Memory 变化通知。
    /// </summary>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <returns>可等待的通知 listener；宿主尚未发布或平台不支持时为空。</returns>
    public SharedMemoryTelemetryNotificationListener? CreateTelemetryNotificationListener(string engineId)
    {
        ThrowIfDisposed();
        return SharedMemoryTelemetryNotificationListener.TryOpen(
            Paths.ProjectRoot,
            engineId,
            out var listener,
            out _)
            ? listener
            : null;
    }

    /// <summary>获取已校验的 segment 名称与 engine 哈希，避免 100ms 空闲路径重复拼接和计算。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">Telemetry 名称。</param>
    /// <returns>项目生命周期内可复用的只读目标。</returns>
    private TelemetryReadTarget GetTelemetryReadTarget(string engineId, string kit, string name)
    {
        TelemetryTargetKey key = new(engineId, kit, name);
        if (mTelemetryTargets.TryGetValue(key, out var target))
        {
            return target;
        }

        if (mTelemetryTargets.Count >= MAX_CACHED_TELEMETRY_TARGETS)
        {
            ClearTelemetryTargets();
        }

        target = new TelemetryReadTarget(
            SharedMemoryTelemetrySegmentName.Create(Paths.ProjectRoot, engineId, kit, name),
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId));
        mTelemetryTargets.Add(key, target);
        return target;
    }

    /// <summary>释放并清空全部 telemetry 目标；调用方必须已持有缓存锁。</summary>
    private void ClearTelemetryTargets()
    {
        List<Exception>? failures = null;
        foreach (var target in mTelemetryTargets.Values)
        {
            try
            {
                target.Dispose();
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }
        }

        mTelemetryTargets.Clear();
        if (failures != null)
        {
            throw new AggregateException("Telemetry targets could not be fully disposed.", failures);
        }
    }

    /// <summary>获取当前缓存目标数，供有界缓存回归测试验证。</summary>
    internal int CachedTelemetryTargetCount
    {
        get
        {
            lock (mTelemetryTargetsGate)
            {
                return mTelemetryTargets.Count;
            }
        }
    }

    /// <summary>获取当前仍持有 OS accessor 的 lease 数，供生命周期回归测试验证。</summary>
    internal int ActiveTelemetryLeaseCount
    {
        get
        {
            lock (mTelemetryTargetsGate)
            {
                var count = 0;
                foreach (var target in mTelemetryTargets.Values)
                {
                    count += target.HasOpenLease ? 1 : 0;
                }

                return count;
            }
        }
    }

    /// <summary>读取指定目标累计成功打开 map/accessor 的次数，供空闲热路径回归测试验证。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">Telemetry 名称。</param>
    /// <returns>目标仍在缓存时返回累计次数，否则返回 0。</returns>
    internal int GetTelemetryMapOpenCount(string engineId, string kit, string name)
    {
        lock (mTelemetryTargetsGate)
        {
            TelemetryTargetKey key = new(engineId, kit, name);
            return mTelemetryTargets.TryGetValue(key, out var target) ? target.OpenCount : 0;
        }
    }

    /// <summary>
    /// 根据最新 engine registry 判断指定命令是否由当前 endpoint 明确声明为只读加速能力。
    /// </summary>
    public bool CanSendFastChannelReadOnlyCommand(string engineId, string kit, string action)
    {
        ThrowIfDisposed();
        return mFastChannelCommandTransport.CanSendReadOnlyCommand(engineId, kit, action);
    }

    /// <summary>
    /// 释放指定 engine 的缓存 FastChannel 连接，避免生命周期重建期间复用旧 stream。
    /// </summary>
    /// <param name="engineId">需要失效连接的 engine。</param>
    public Task InvalidateFastChannelConnectionsAsync(string engineId)
    {
        ThrowIfDisposed();
        return mFastChannelCommandTransport.InvalidateConnectionsAsync(engineId);
    }

    /// <summary>
    /// 通过 FastChannel 发送当前 endpoint 明确声明的通用只读命令。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标只读 action。</param>
    /// <param name="payloadJson">查询 payload JSON。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">调用方为本次快速通道操作分配的本地最大等待毫秒数；线上信封会单独遵守协议超时范围。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>FastChannel Host 直接返回的 terminal response。</returns>
    public Task<CommandResponse> SendFastChannelReadOnlyCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return mFastChannelCommandTransport.SendReadOnlyCommandAsync(
            engineId,
            kit,
            action,
            payloadJson,
            source,
            timeoutMs,
            cancellationToken);
    }

    /// <summary>
    /// 通过 Client 管理的本机 FastChannel 发送只读 System 命令；连接失败、生命周期不匹配或协议异常由调用侧选择可靠回退。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="action">仅允许 ping 或 bridge_status。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">调用方为本次快速通道操作分配的本地最大等待毫秒数；线上信封会单独遵守协议超时范围。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>FastChannel Host 直接返回的 terminal response。</returns>
    public Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
        string engineId,
        string action,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return mFastChannelCommandTransport.SendReadOnlyCommandAsync(
            engineId,
            "System",
            action,
            "{}",
            source,
            timeoutMs,
            cancellationToken);
    }

    /// <summary>
    /// 通过可靠 FileBridge 写入命令并等待 terminal response。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">payload JSON。</param>
    /// <param name="source">审计来源；不作为身份认证。</param>
    /// <param name="timeoutMs">等待响应的超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令信封、证据路径和 terminal response。</returns>
    public Task<CommandSendResult> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return mFileBridgeTransport.SendCommandAsync(
            engineId,
            kit,
            action,
            payloadJson,
            source,
            timeoutMs,
            cancellationToken);
    }

    /// <summary>作为有界缓存键保存 engine、Kit 和 Telemetry 名称。</summary>
    private readonly record struct TelemetryTargetKey(string EngineId, string Kit, string Name);

    /// <summary>保存单个 Telemetry 目标，并确保 map lease 只在同一 generation 内复用。</summary>
    private sealed class TelemetryReadTarget : IDisposable
    {
        private readonly object mGate = new();
        private readonly string mSegmentName;
        private readonly ulong mEngineIdHash;
        private SharedMemoryTelemetryNamedMapLease? mLease;
        private long? mLeaseGeneration;
        private int mClosedLeaseOpenCount;

        /// <summary>创建已完成 segment 名称和 engine 哈希计算的缓存目标。</summary>
        /// <param name="segmentName">完整 segment 名称。</param>
        /// <param name="engineIdHash">预期 engine 哈希。</param>
        public TelemetryReadTarget(string segmentName, ulong engineIdHash)
        {
            mSegmentName = segmentName;
            mEngineIdHash = engineIdHash;
        }

        /// <summary>获取目标生命周期内累计成功打开 map/accessor 的次数。</summary>
        public int OpenCount
        {
            get
            {
                lock (mGate)
                {
                    return mClosedLeaseOpenCount + (mLease?.OpenCount ?? 0);
                }
            }
        }

        /// <summary>获取当前 generation 是否持有已打开的 accessor。</summary>
        public bool HasOpenLease
        {
            get
            {
                lock (mGate)
                {
                    return mLease?.IsOpen == true;
                }
            }
        }

        /// <summary>读取当前完整帧，并在 generation 改变前先释放旧映射。</summary>
        /// <param name="expectedGeneration">当前宿主 generation。</param>
        /// <param name="maxPayloadBytes">payload 最大字节数。</param>
        /// <returns>帧读取结果。</returns>
        public SharedMemoryTelemetryFrameReadResult Read(long? expectedGeneration, int maxPayloadBytes)
        {
            lock (mGate)
            {
                return GetLease(expectedGeneration).Read(maxPayloadBytes);
            }
        }

        /// <summary>增量读取新帧，并在 generation 改变前先释放旧映射。</summary>
        /// <param name="expectedGeneration">当前宿主 generation。</param>
        /// <param name="maxPayloadBytes">payload 最大字节数。</param>
        /// <param name="afterSequence">最后接受的帧序号。</param>
        /// <returns>新帧或失败结果；稳定未变化时返回空。</returns>
        public SharedMemoryTelemetryFrameReadResult? ReadIfChanged(
            long? expectedGeneration,
            int maxPayloadBytes,
            long afterSequence)
        {
            lock (mGate)
            {
                return GetLease(expectedGeneration).ReadIfChanged(maxPayloadBytes, afterSequence);
            }
        }

        /// <summary>复用同代 lease；generation 改变时先完整释放旧 map、accessor 与缓冲区。</summary>
        /// <param name="expectedGeneration">当前宿主 generation。</param>
        /// <returns>与 segment 和 generation 完全绑定的 lease。</returns>
        private SharedMemoryTelemetryNamedMapLease GetLease(long? expectedGeneration)
        {
            if (mLease != null && mLeaseGeneration == expectedGeneration)
            {
                return mLease;
            }

            ReleaseLease();
            mLeaseGeneration = expectedGeneration;
            mLease = new SharedMemoryTelemetryNamedMapLease(
                mSegmentName,
                expectedGeneration,
                mEngineIdHash);
            return mLease;
        }

        /// <summary>累加诊断计数并释放当前 generation 的 lease。</summary>
        private void ReleaseLease()
        {
            if (mLease == null)
            {
                return;
            }

            mClosedLeaseOpenCount += mLease.OpenCount;
            mLease.Dispose();
            mLease = null;
            mLeaseGeneration = null;
        }

        /// <summary>释放当前目标持有的 map、accessor 与 ArrayPool 缓冲区。</summary>
        public void Dispose()
        {
            lock (mGate)
            {
                ReleaseLease();
            }
        }
    }
}
