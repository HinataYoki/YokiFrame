namespace YokiFrame.Tooling.Application.Models.UIKit;

/// <summary>描述 UIKit 状态来源、宿主身份和原始 payload。</summary>
internal sealed record WorkbenchUIKitDataSource(
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
    /// <param name="reason">本轮解析失败原因。</param>
    /// <returns>附加 stale 原因的新数据源。</returns>
    internal WorkbenchUIKitDataSource WithStaleReason(string reason)
    {
        string combined = string.IsNullOrWhiteSpace(StaleReason)
            ? reason
            : StaleReason + " " + reason;
        return this with { StaleReason = combined };
    }
}
