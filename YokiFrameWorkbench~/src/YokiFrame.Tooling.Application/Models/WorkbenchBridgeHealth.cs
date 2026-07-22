namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench 首屏展示和重连判断需要的 FileBridge 健康信息。
/// </summary>
public sealed class WorkbenchBridgeHealth
{
    /// <summary>
    /// 创建 FileBridge 健康信息。
    /// </summary>
    /// <param name="state">连接状态归类。</param>
    /// <param name="message">面向用户的状态说明。</param>
    /// <param name="suggestion">恢复或排查建议。</param>
    /// <param name="evidencePaths">可用于排查的证据路径。</param>
    /// <param name="heartbeatAgeSeconds">heartbeat 年龄；无 heartbeat 时为 null。</param>
    /// <param name="staleThresholdSeconds">stale 判定阈值秒数。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生成代号。</param>
    /// <param name="mode">宿主模式。</param>
    /// <param name="sequence">heartbeat 序号。</param>
    public WorkbenchBridgeHealth(
        WorkbenchBridgeConnectionState state,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths,
        long? heartbeatAgeSeconds,
        long staleThresholdSeconds,
        string sessionId,
        long generation,
        string mode,
        long sequence)
    {
        State = state;
        Message = message;
        Suggestion = suggestion;
        EvidencePaths = evidencePaths;
        HeartbeatAgeSeconds = heartbeatAgeSeconds;
        StaleThresholdSeconds = staleThresholdSeconds;
        SessionId = sessionId;
        Generation = generation;
        Mode = mode;
        Sequence = sequence;
    }

    /// <summary>
    /// 获取连接状态归类。
    /// </summary>
    public WorkbenchBridgeConnectionState State { get; }

    /// <summary>
    /// 获取面向用户的状态说明。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取恢复或排查建议。
    /// </summary>
    public string Suggestion { get; }

    /// <summary>
    /// 获取可用于排查的证据路径。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>
    /// 获取 heartbeat 年龄；无 heartbeat 时为 null。
    /// </summary>
    public long? HeartbeatAgeSeconds { get; }

    /// <summary>
    /// 获取 stale 判定阈值秒数。
    /// </summary>
    public long StaleThresholdSeconds { get; }

    /// <summary>
    /// 获取宿主会话标识。
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// 获取宿主生成代号。
    /// </summary>
    public long Generation { get; }

    /// <summary>
    /// 获取宿主模式。
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// 获取 heartbeat 序号。
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// 获取当前状态是否需要用户或宿主侧重连/恢复。
    /// </summary>
    public bool RequiresReconnect => State != WorkbenchBridgeConnectionState.Online;
}
