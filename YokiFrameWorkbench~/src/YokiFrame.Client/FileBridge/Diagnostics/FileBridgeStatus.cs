using System.Text.Json.Nodes;

namespace YokiFrame.Client.FileBridge.Diagnostics;

/// <summary>
/// 汇总指定 engine 的 FileBridge 队列、响应和 heartbeat 状态。
/// </summary>
public sealed class FileBridgeStatus
{
    /// <summary>
    /// 创建 bridge 状态快照。
    /// </summary>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="engineRoot">engine 根目录。</param>
    /// <param name="commandsRoot">commands 目录。</param>
    /// <param name="resultsRoot">results 目录。</param>
    public FileBridgeStatus(string engineId, string engineRoot, string commandsRoot, string resultsRoot)
    {
        EngineId = engineId;
        EngineRoot = engineRoot;
        CommandsRoot = commandsRoot;
        ResultsRoot = resultsRoot;
    }

    /// <summary>
    /// 获取 engine 标识。
    /// </summary>
    public string EngineId { get; }

    /// <summary>
    /// 获取 engine 根目录。
    /// </summary>
    public string EngineRoot { get; }

    /// <summary>
    /// 获取 commands 目录。
    /// </summary>
    public string CommandsRoot { get; }

    /// <summary>
    /// 获取 results 目录。
    /// </summary>
    public string ResultsRoot { get; }

    /// <summary>
    /// 获取或设置 pending 命令数量。
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// 获取或设置 processing 命令数量。
    /// </summary>
    public int ProcessingCount { get; set; }

    /// <summary>
    /// 获取或设置 archive 命令数量。
    /// </summary>
    public int ArchiveCount { get; set; }

    /// <summary>
    /// 获取或设置 deadletter 文件数量。
    /// </summary>
    public int DeadletterCount { get; set; }

    /// <summary>
    /// 获取或设置 response 文件数量。
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// 获取或设置当前 engine 协议目录下的 JSON 文件总数。
    /// </summary>
    public int ProtocolFileCount { get; set; }

    /// <summary>
    /// 获取或设置当前 engine 协议目录下 JSON 文件占用的总字节数。
    /// </summary>
    public long ProtocolBytes { get; set; }

    /// <summary>
    /// 获取或设置当前 engine 协议目录下最旧 JSON 文件的最后写入时间。
    /// </summary>
    public DateTimeOffset? OldestProtocolFileUtc { get; set; }

    /// <summary>
    /// 获取或设置 FileBridge 是否处于背压状态；没有宿主上报时保持 false。
    /// </summary>
    public bool BackpressureActive { get; set; }

    /// <summary>
    /// 获取或设置最近一次轮询限流原因；没有宿主上报时为空字符串。
    /// </summary>
    public string LastPollLimitReason { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置宿主报告的 BridgeBusy 次数；没有宿主上报时保持 0。
    /// </summary>
    public int BridgeBusyCount { get; set; }

    /// <summary>
    /// 获取或设置最近一次 FileBridge 错误；没有宿主上报时为空字符串。
    /// </summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 heartbeat 信息。
    /// </summary>
    public HeartbeatInfo? Heartbeat { get; set; }

    /// <summary>
    /// 获取或设置证据目录保留策略。
    /// </summary>
    public FileBridgeRetentionInfo Retention { get; set; } = FileBridgeRetentionInfo.CreateDefault();

    /// <summary>
    /// 转换为 CLI 输出使用的 JSON 对象。
    /// </summary>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <param name="staleThreshold">heartbeat stale 阈值。</param>
    /// <returns>bridge 状态 JSON。</returns>
    public JsonObject ToJson(DateTimeOffset nowUtc, TimeSpan staleThreshold)
    {
        return new JsonObject
        {
            ["engineId"] = EngineId,
            ["engineRoot"] = EngineRoot,
            ["commandsRoot"] = CommandsRoot,
            ["resultsRoot"] = ResultsRoot,
            ["pending"] = PendingCount,
            ["processing"] = ProcessingCount,
            ["archive"] = ArchiveCount,
            ["deadletter"] = DeadletterCount,
            ["results"] = ResultCount,
            ["protocolFileCount"] = ProtocolFileCount,
            ["protocolBytes"] = ProtocolBytes,
            ["oldestProtocolFileUtc"] = OldestProtocolFileUtc?.UtcDateTime.ToString("O"),
            ["backpressureActive"] = BackpressureActive,
            ["lastPollLimitReason"] = LastPollLimitReason,
            ["bridgeBusyCount"] = BridgeBusyCount,
            ["lastError"] = LastError,
            ["retention"] = Retention.ToJson(),
            ["heartbeat"] = Heartbeat?.ToJson(nowUtc, staleThreshold)
        };
    }
}
