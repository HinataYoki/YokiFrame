namespace YokiFrame.Tooling.Application.Models.PoolKit;

/// <summary>描述 PoolKit 状态来源、宿主身份和原始 payload。</summary>
internal sealed record WorkbenchPoolKitDataSource(
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
    /// <summary>追加解析错误并保留原始来源证据。</summary>
    internal WorkbenchPoolKitDataSource WithStaleReason(string reason)
    {
        string combined = string.IsNullOrWhiteSpace(StaleReason) ? reason : StaleReason + " " + reason;
        return this with { StaleReason = combined ?? string.Empty };
    }
}
