namespace YokiFrame.Tooling.Application.Models.SaveKit;

/// <summary>描述 SaveKit Runtime state 的来源、宿主身份和原始 payload。</summary>
internal sealed record WorkbenchSaveKitDataSource(
    string EngineId,
    string SessionId,
    long Generation,
    string Mode,
    DateTimeOffset UpdatedAtUtc,
    string Source,
    string Transport,
    IReadOnlyList<string> EvidencePaths,
    string StaleReason,
    string RawPayloadJson)
{
    /// <summary>追加解析错误，同时保留已有传输或身份 stale 信息。</summary>
    internal WorkbenchSaveKitDataSource WithStaleReason(string reason)
    {
        string combined = string.IsNullOrWhiteSpace(StaleReason)
            ? reason
            : StaleReason + " " + reason;
        return this with { StaleReason = combined ?? string.Empty };
    }
}
