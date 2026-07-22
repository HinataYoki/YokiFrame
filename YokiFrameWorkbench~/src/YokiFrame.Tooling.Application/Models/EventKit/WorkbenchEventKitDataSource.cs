namespace YokiFrame.Tooling.Application.Models.EventKit;

/// <summary>
/// 描述 EventKit 状态读取的宿主身份、来源、证据和原始 payload。
/// </summary>
internal sealed class WorkbenchEventKitDataSource
{
    /// <summary>创建不可变 EventKit 数据源元数据。</summary>
    internal WorkbenchEventKitDataSource(
        string engineId,
        string sessionId,
        long generation,
        string mode,
        DateTimeOffset updatedAtUtc,
        string source,
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
        EvidencePaths = evidencePaths?.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray()
            ?? Array.Empty<string>();
        StaleReason = staleReason ?? string.Empty;
        RawPayloadJson = rawPayloadJson ?? string.Empty;
    }

    internal string EngineId { get; }
    internal string SessionId { get; }
    internal long Generation { get; }
    internal string Mode { get; }
    internal DateTimeOffset UpdatedAtUtc { get; }
    internal string Source { get; }
    internal IReadOnlyList<string> EvidencePaths { get; }
    internal string StaleReason { get; }
    internal string RawPayloadJson { get; }

    /// <summary>追加解析失败原因并保留其余来源信息。</summary>
    internal WorkbenchEventKitDataSource WithStaleReason(string staleReason)
    {
        return new WorkbenchEventKitDataSource(
            EngineId,
            SessionId,
            Generation,
            Mode,
            UpdatedAtUtc,
            Source,
            EvidencePaths,
            staleReason,
            RawPayloadJson);
    }
}
