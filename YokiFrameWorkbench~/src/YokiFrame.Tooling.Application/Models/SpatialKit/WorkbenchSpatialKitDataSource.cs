namespace YokiFrame.Tooling.Application.Models.SpatialKit;

/// <summary>描述 SpatialKit 状态来源、宿主身份和原始 payload。</summary>
internal sealed record WorkbenchSpatialKitDataSource(
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
    /// <summary>追加解析错误并保留已有来源诊断。</summary>
    internal WorkbenchSpatialKitDataSource WithStaleReason(string reason)
    {
        string combined = string.IsNullOrWhiteSpace(StaleReason)
            ? reason
            : StaleReason + " " + reason;
        return this with { StaleReason = combined ?? string.Empty };
    }
}
