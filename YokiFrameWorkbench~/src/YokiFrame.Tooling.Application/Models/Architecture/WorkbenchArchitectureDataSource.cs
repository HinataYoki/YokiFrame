namespace YokiFrame.Tooling.Application.Models.Architecture;

/// <summary>
/// 描述 Architecture 状态读取的宿主身份、来源、证据和原始 payload。
/// </summary>
internal sealed class WorkbenchArchitectureDataSource
{
    /// <summary>创建不可变 Architecture 数据源元数据。</summary>
    internal WorkbenchArchitectureDataSource(
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

    /// <summary>获取目标 engine 标识。</summary>
    internal string EngineId { get; }

    /// <summary>获取宿主 session 标识。</summary>
    internal string SessionId { get; }

    /// <summary>获取宿主 generation。</summary>
    internal long Generation { get; }

    /// <summary>获取宿主当前模式。</summary>
    internal string Mode { get; }

    /// <summary>获取数据更新时间。</summary>
    internal DateTimeOffset UpdatedAtUtc { get; }

    /// <summary>获取 telemetry 或 snapshot 来源。</summary>
    internal string Source { get; }

    /// <summary>获取状态证据路径。</summary>
    internal IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>获取数据不可用或回落原因。</summary>
    internal string StaleReason { get; }

    /// <summary>获取未经裁剪的 Architecture payload。</summary>
    internal string RawPayloadJson { get; }

    /// <summary>追加解析失败原因并保留其余来源信息。</summary>
    internal WorkbenchArchitectureDataSource WithStaleReason(string staleReason)
    {
        return new WorkbenchArchitectureDataSource(
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
