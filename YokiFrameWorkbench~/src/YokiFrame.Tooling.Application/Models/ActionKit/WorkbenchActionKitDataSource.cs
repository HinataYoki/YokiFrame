namespace YokiFrame.Tooling.Application.Models.ActionKit;

/// <summary>描述 ActionKit 状态来源、宿主身份和原始 payload。</summary>
internal sealed record WorkbenchActionKitDataSource(
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
    /// <param name="reason">新发现的 stale 原因。</param>
    /// <returns>带合并原因的新数据源。</returns>
    internal WorkbenchActionKitDataSource WithStaleReason(string reason)
    {
        string combined = string.IsNullOrWhiteSpace(StaleReason)
            ? reason
            : StaleReason + " " + reason;
        return this with { StaleReason = combined ?? string.Empty };
    }
}
