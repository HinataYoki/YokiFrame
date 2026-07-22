namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>描述 LogKit 状态来源、宿主身份和原始 payload。</summary>
internal sealed record WorkbenchLogKitDataSource(
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
    /// <param name="reason">要追加的错误。</param>
    /// <returns>带新错误的数据源。</returns>
    internal WorkbenchLogKitDataSource WithStaleReason(string reason)
    {
        var combined = string.IsNullOrWhiteSpace(StaleReason) ? reason : StaleReason + " " + reason;
        return this with { StaleReason = combined ?? string.Empty };
    }
}
