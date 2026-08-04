using System.Text.Json.Serialization;

namespace YokiFrame.Protocol.FileBridge;

/// <summary>
/// 描述 FileBridge 请求在可靠控制面上的可观察状态。
/// </summary>
public enum CommandRequestState
{
    /// <summary>请求仍在 pending 队列中。</summary>
    Pending,

    /// <summary>请求已经被某个 Host 原子认领并正在处理。</summary>
    Processing,

    /// <summary>请求已经写入成功 terminal response。</summary>
    Succeeded,

    /// <summary>请求已经写入失败 terminal response。</summary>
    Failed,

    /// <summary>请求无法消费，原始请求和 deadletter 诊断已经保留。</summary>
    Deadletter,

    /// <summary>processing lease 已过期，Host 未自动重放原始请求。</summary>
    Expired,

    /// <summary>请求可能已经执行，但 terminal response 无法确认或写入。</summary>
    Unknown,

    /// <summary>当前证据目录中没有找到该请求。</summary>
    NotFound
}

/// <summary>
/// 表示一次 request status 查询的结果和可复查证据。
/// </summary>
public sealed class CommandRequestStatus
{
    /// <summary>获取或设置协议版本。</summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; }

    /// <summary>获取或设置请求标识。</summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>获取或设置目标 engine 标识。</summary>
    [JsonPropertyName("engineId")]
    public string EngineId { get; set; } = string.Empty;

    /// <summary>获取或设置请求状态。</summary>
    [JsonPropertyName("state")]
    public CommandRequestState State { get; set; }

    /// <summary>获取或设置最近一次可观察更新时间。</summary>
    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    /// <summary>获取或设置当前状态对应的响应；非 terminal 状态为空。</summary>
    [JsonPropertyName("response")]
    public CommandResponse? Response { get; set; }

    /// <summary>获取或设置状态证据路径。</summary>
    [JsonPropertyName("evidencePaths")]
    public IReadOnlyList<string> EvidencePaths { get; set; } = Array.Empty<string>();

    /// <summary>获取当前状态是否已经终态化。</summary>
    [JsonIgnore]
    public bool IsTerminal => State is CommandRequestState.Succeeded
        or CommandRequestState.Failed
        or CommandRequestState.Deadletter
        or CommandRequestState.Expired
        or CommandRequestState.Unknown;
}
