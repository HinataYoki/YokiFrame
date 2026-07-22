using System.Text.Json.Nodes;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client;

/// <summary>
/// 定义 Workbench、Installer 和 CLI 访问 YokiFrame 宿主状态与命令的统一客户端边界。
/// </summary>
public interface IYokiFrameClient
{
    /// <summary>
    /// 获取当前项目的 YokiFrame 标准路径解析器。
    /// </summary>
    YokiFramePaths Paths { get; }

    /// <summary>
    /// 读取 harness capability 文件。
    /// </summary>
    /// <returns>capability JSON 节点。</returns>
    JsonNode ReadHarnessCapabilities();

    /// <summary>
    /// 读取当前项目注册的全部 engine。
    /// </summary>
    /// <returns>engine registry 条目。</returns>
    IReadOnlyList<EngineRegistryEntry> ReadEngineEntries();

    /// <summary>
    /// 读取指定 Kit 的 snapshot。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <returns>snapshot JSON 节点。</returns>
    JsonNode ReadSnapshot(string engineId, string kit, string name);

    /// <summary>
    /// 读取指定 engine 的 heartbeat。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>heartbeat；文件不存在时返回 null。</returns>
    HeartbeatInfo? ReadHeartbeat(string engineId);

    /// <summary>
    /// 汇总指定 engine 的 FileBridge 状态。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>FileBridge 状态。</returns>
    FileBridgeStatus ReadBridgeStatus(string engineId);

    /// <summary>
    /// 读取指定 engine、Kit 和状态名的 Shared Memory telemetry 帧。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">状态名称。</param>
    /// <param name="expectedGeneration">期望 generation；为空时不校验。</param>
    /// <param name="maxPayloadBytes">允许的最大 payload 字节数。</param>
    /// <returns>telemetry 帧读取结果。</returns>
    SharedMemoryTelemetryFrameReadResult ReadTelemetry(
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        int maxPayloadBytes);

    /// <summary>
    /// 读取晚于指定游标的 Shared Memory telemetry 帧；稳定未变化帧返回空，供高频响应式刷新跳过重复 payload。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">状态名称。</param>
    /// <param name="expectedGeneration">期望 generation；为空时不校验。</param>
    /// <param name="maxPayloadBytes">允许的最大 payload 字节数。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号。</param>
    /// <returns>新帧或读取失败结果；帧未变化时返回空。</returns>
    SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        int maxPayloadBytes,
        long afterSequence);

    /// <summary>
    /// 尝试打开当前项目和 engine 的 Shared Memory 变化通知；不可用时返回 null，调用方必须保留周期刷新兜底。
    /// </summary>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <returns>可等待的项目级通知 listener；宿主未发布或平台不支持时为空。</returns>
    SharedMemoryTelemetryNotificationListener? CreateTelemetryNotificationListener(string engineId)
    {
        return null;
    }

    /// <summary>
    /// 判断当前 registry 是否明确声明指定 Kit/action 可通过 FastChannel 执行。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <returns>当前 endpoint 启用且声明该命令时返回 true。</returns>
    bool CanSendFastChannelReadOnlyCommand(string engineId, string kit, string action)
    {
        return false;
    }

    /// <summary>
    /// 立即丢弃指定 engine 的 FastChannel 连接；生命周期文件变化时由 Application 层调用。
    /// </summary>
    /// <param name="engineId">需要失效连接的 engine。</param>
    Task InvalidateFastChannelConnectionsAsync(string engineId)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通过当前 registry 中明确声明的 FastChannel endpoint 发送通用只读命令。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">允许携带查询参数的 payload JSON。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令期限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Host 直接返回的 terminal response。</returns>
    Task<CommandResponse> SendFastChannelReadOnlyCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The client does not implement generic FastChannel read-only commands.");
    }

    /// <summary>
    /// 通过当前 registry 中可用的 FastChannel 发送只读 System 命令。
    /// Client 负责 endpoint 选择、连接缓存、session/generation 重连和响应校验；
    /// 调用侧在该可选优化失败时应回退到 <see cref="SendCommandAsync"/>。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="action">仅允许 ping 或 bridge_status。</param>
    /// <param name="source">审计来源；不作为身份认证。</param>
    /// <param name="timeoutMs">命令和快速通道操作的最大等待毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Host 直接返回的 terminal response，不产生 FileBridge 文件证据。</returns>
    Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
        string engineId,
        string action,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken);

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
    Task<CommandSendResult> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken);
}
