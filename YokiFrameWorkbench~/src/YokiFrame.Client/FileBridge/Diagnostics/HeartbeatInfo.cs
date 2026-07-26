using System.Text.Json.Nodes;

namespace YokiFrame.Client.FileBridge.Diagnostics;

/// <summary>
/// 描述 engine heartbeat 文件中的时间信息和 stale 判断。
/// </summary>
public sealed class HeartbeatInfo
{
    /// <summary>
    /// 创建 heartbeat 信息。
    /// </summary>
    /// <param name="path">heartbeat 文件路径。</param>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="createdAtUtc">heartbeat 写入时间。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生成代号，用于识别重启或 Domain Reload 后的新实例。</param>
    /// <param name="mode">宿主当前模式，例如 EditMode 或 PlayMode。</param>
    /// <param name="sequence">heartbeat 序号。</param>
    public HeartbeatInfo(
        string path,
        string engineId,
        DateTimeOffset createdAtUtc,
        string sessionId = "",
        long generation = 0L,
        string mode = "",
        long sequence = 0L)
    {
        Path = path;
        EngineId = engineId;
        CreatedAtUtc = createdAtUtc;
        SessionId = sessionId;
        Generation = generation;
        Mode = mode;
        Sequence = sequence;
    }

    /// <summary>
    /// 获取 heartbeat 文件路径。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 获取 engine 标识。
    /// </summary>
    public string EngineId { get; }

    /// <summary>
    /// 获取 heartbeat 写入时间。
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// 获取宿主会话标识。
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// 获取宿主生成代号。
    /// </summary>
    public long Generation { get; }

    /// <summary>
    /// 获取宿主当前模式。
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// 获取 heartbeat 序号。
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// 从 heartbeat JSON 节点创建时间信息。
    /// </summary>
    /// <param name="path">heartbeat 文件路径。</param>
    /// <param name="node">heartbeat JSON 节点。</param>
    /// <returns>解析后的 heartbeat 信息。</returns>
    public static HeartbeatInfo FromJson(string path, JsonNode node)
    {
        var engineId = node["engineId"]?.GetValue<string>() ?? string.Empty;
        var createdAtText = node["createdAtUtc"]?.GetValue<string>()
            ?? node["writtenAtUtc"]?.GetValue<string>()
            ?? node["updatedAtUtc"]?.GetValue<string>();
        var sessionId = GetString(node, "sessionId");
        var generation = GetInt64(node, "generation");
        var mode = GetString(node, "mode");
        var sequence = GetInt64(node, "sequence");
        if (DateTimeOffset.TryParse(createdAtText, out var createdAtUtc))
        {
            return new HeartbeatInfo(path, engineId, createdAtUtc.ToUniversalTime(), sessionId, generation, mode, sequence);
        }

        var timestamp = node["timestamp"]?.GetValue<long>() ?? 0L;
        var fallbackTime = timestamp > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp)
            : DateTimeOffset.MinValue;
        return new HeartbeatInfo(path, engineId, fallbackTime, sessionId, generation, mode, sequence);
    }

    /// <summary>
    /// 判断 heartbeat 是否已经超过 stale 阈值。
    /// </summary>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <param name="staleThreshold">stale 阈值。</param>
    /// <returns>超过阈值或时间无效时返回 true。</returns>
    public bool IsStale(DateTimeOffset nowUtc, TimeSpan staleThreshold)
    {
        if (CreatedAtUtc == DateTimeOffset.MinValue)
        {
            return true;
        }

        return nowUtc - CreatedAtUtc > staleThreshold;
    }

    /// <summary>
    /// 转换为 CLI 输出使用的 JSON 对象。
    /// </summary>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <param name="staleThreshold">stale 阈值。</param>
    /// <returns>heartbeat 状态 JSON。</returns>
    public JsonObject ToJson(DateTimeOffset nowUtc, TimeSpan staleThreshold)
    {
        return new JsonObject
        {
            ["path"] = Path,
            ["engineId"] = EngineId,
            ["createdAtUtc"] = CreatedAtUtc.ToString("O"),
            ["sessionId"] = SessionId,
            ["generation"] = Generation,
            ["mode"] = Mode,
            ["sequence"] = Sequence,
            ["ageSeconds"] = Math.Max(0, (long)(nowUtc - CreatedAtUtc).TotalSeconds),
            ["staleThresholdSeconds"] = (long)staleThreshold.TotalSeconds,
            ["isStale"] = IsStale(nowUtc, staleThreshold)
        };
    }

    /// <summary>
    /// 从 JSON 节点读取字符串字段，缺失或类型不匹配时返回空字符串。
    /// </summary>
    /// <param name="node">heartbeat JSON 节点。</param>
    /// <param name="name">字段名。</param>
    /// <returns>字段文本。</returns>
    private static string GetString(JsonNode node, string name)
    {
        try
        {
            return node[name]?.GetValue<string>() ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 从 JSON 节点读取长整数字段，缺失、字符串为空或格式不兼容时返回 0。
    /// </summary>
    /// <param name="node">heartbeat JSON 节点。</param>
    /// <param name="name">字段名。</param>
    /// <returns>长整数值。</returns>
    private static long GetInt64(JsonNode node, string name)
    {
        var value = node[name];
        if (value == null)
        {
            return 0L;
        }

        try
        {
            return value.GetValue<long>();
        }
        catch (InvalidOperationException)
        {
            return long.TryParse(value.ToString(), out var parsedValue) ? parsedValue : 0L;
        }
    }
}
