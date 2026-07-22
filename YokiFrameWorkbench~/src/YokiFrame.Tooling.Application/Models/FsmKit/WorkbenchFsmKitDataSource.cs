namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>
/// 描述一次 FsmKit 状态读取的数据来源、宿主身份、证据和原始 payload。
/// </summary>
public sealed class WorkbenchFsmKitDataSource
{
    /// <summary>
    /// 创建不可变 FsmKit 数据源元数据；只允许 Application 用例构造。
    /// </summary>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主 generation。</param>
    /// <param name="mode">宿主当前模式。</param>
    /// <param name="updatedAtUtc">数据源更新时间。</param>
    /// <param name="source">telemetry、snapshot 或 command。</param>
    /// <param name="transport">显式命令实际使用的传输；周期读取时为空。</param>
    /// <param name="evidencePaths">状态或命令证据路径。</param>
    /// <param name="staleReason">数据不可用或发生回落的原因。</param>
    /// <param name="rawPayloadJson">未经裁剪的 FsmKit payload。</param>
    internal WorkbenchFsmKitDataSource(
        string engineId,
        string sessionId,
        long generation,
        string mode,
        DateTimeOffset updatedAtUtc,
        string source,
        string transport,
        IReadOnlyList<string> evidencePaths,
        string staleReason,
        string rawPayloadJson)
    {
        EngineId = engineId ?? string.Empty;
        SessionId = sessionId ?? string.Empty;
        Generation = generation;
        Mode = mode ?? string.Empty;
        UpdatedAtUtc = updatedAtUtc;
        Source = source ?? string.Empty;
        Transport = transport ?? string.Empty;
        EvidencePaths = evidencePaths?.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray()
            ?? Array.Empty<string>();
        StaleReason = staleReason ?? string.Empty;
        RawPayloadJson = rawPayloadJson ?? string.Empty;
    }

    /// <summary>获取目标 engine 标识。</summary>
    public string EngineId { get; }

    /// <summary>获取宿主会话标识。</summary>
    public string SessionId { get; }

    /// <summary>获取宿主 generation。</summary>
    public long Generation { get; }

    /// <summary>获取宿主当前模式。</summary>
    public string Mode { get; }

    /// <summary>获取数据源更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    /// <summary>获取 telemetry、snapshot 或 command 来源标识。</summary>
    public string Source { get; }

    /// <summary>获取显式命令实际使用的传输；周期读取时为空。</summary>
    public string Transport { get; }

    /// <summary>获取状态文件、内存段或命令响应证据。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>获取数据不可用或回落原因；正常状态为空。</summary>
    public string StaleReason { get; }

    /// <summary>获取未经裁剪的 FsmKit payload。</summary>
    public string RawPayloadJson { get; }

    /// <summary>
    /// 在解析失败时保留原始来源信息并追加 stale 原因。
    /// </summary>
    /// <param name="staleReason">解析或读取失败原因。</param>
    /// <returns>带新 stale 原因的数据源副本。</returns>
    internal WorkbenchFsmKitDataSource WithStaleReason(string staleReason)
    {
        return new WorkbenchFsmKitDataSource(
            EngineId,
            SessionId,
            Generation,
            Mode,
            UpdatedAtUtc,
            Source,
            Transport,
            EvidencePaths,
            staleReason,
            RawPayloadJson);
    }
}
