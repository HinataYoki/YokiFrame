using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.FastChannel;

/// <summary>
/// 描述 engine registry 中的可选 FastChannel endpoint，并提供 session / generation 重连判断。
/// </summary>
public sealed class FastChannelEndpoint
{
    /// <summary>
    /// FileBridge fallback 名称；FastChannel 不可用时必须回落到该可靠控制面。
    /// </summary>
    public const string FILEBRIDGE_FALLBACK = "filebridge";

    /// <summary>
    /// 获取或设置 FastChannel endpoint 协议版本。
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = 1;

    /// <summary>
    /// 获取或设置 engine 标识。
    /// </summary>
    [JsonPropertyName("engineId")]
    public string EngineId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置宿主会话标识；会话变化时旧连接必须丢弃。
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置宿主生成代号；PlayMode 或 Domain Reload 后通常会递增。
    /// </summary>
    [JsonPropertyName("generation")]
    public long Generation { get; set; }

    /// <summary>
    /// 获取或设置传输类型，使用 <see cref="FastChannelTransport"/> 中的字符串常量。
    /// </summary>
    [JsonPropertyName("transport")]
    public string Transport { get; set; } = FastChannelTransport.None;

    /// <summary>
    /// 获取或设置传输端点，例如 pipe 名称或 Unix socket 路径。
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置该 endpoint 当前是否可被工具侧尝试连接。
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// 获取或设置 FastChannel 不可用时的回落通道名称。
    /// </summary>
    [JsonPropertyName("fallback")]
    public string Fallback { get; set; } = FILEBRIDGE_FALLBACK;

    /// <summary>
    /// 获取或设置 Host 当前明确允许通过该 endpoint 执行的只读 Kit/action 能力键。
    /// </summary>
    [JsonPropertyName("readOnlyCommands")]
    public List<string> ReadOnlyCommands { get; set; } = new();

    /// <summary>
    /// 判断 endpoint 是否明确声明指定命令可通过 FastChannel 执行。
    /// </summary>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <returns>endpoint 启用且能力清单包含该命令时返回 true。</returns>
    public bool SupportsReadOnlyCommand(string kit, string action)
    {
        return Enabled && ReadOnlyCommands.Contains(
            YokiFrameFastChannelContract.CreateCommandKey(kit, action),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// 创建 Windows Named Pipe endpoint 描述。
    /// </summary>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生成代号。</param>
    /// <param name="pipeName">Named Pipe 名称。</param>
    /// <returns>启用状态的 endpoint 描述。</returns>
    public static FastChannelEndpoint CreateNamedPipe(string engineId, string sessionId, long generation, string pipeName)
    {
        return CreateEnabled(engineId, sessionId, generation, FastChannelTransport.NamedPipe, pipeName);
    }

    /// <summary>
    /// 创建 Unix Domain Socket endpoint 描述。
    /// </summary>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生成代号。</param>
    /// <param name="socketPath">Unix socket 路径。</param>
    /// <returns>启用状态的 endpoint 描述。</returns>
    public static FastChannelEndpoint CreateUnixDomainSocket(string engineId, string sessionId, long generation, string socketPath)
    {
        return CreateEnabled(engineId, sessionId, generation, FastChannelTransport.UnixDomainSocket, socketPath);
    }

    /// <summary>
    /// 创建禁用状态 endpoint；调用侧应直接使用 FileBridge fallback。
    /// </summary>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生成代号。</param>
    /// <returns>禁用状态的 endpoint 描述。</returns>
    public static FastChannelEndpoint Disabled(string engineId, string sessionId, long generation)
    {
        return new FastChannelEndpoint
        {
            EngineId = engineId,
            SessionId = sessionId,
            Generation = generation,
            Transport = FastChannelTransport.None,
            Endpoint = string.Empty,
            Enabled = false,
            Fallback = FILEBRIDGE_FALLBACK
        };
    }

    /// <summary>
    /// 判断当前 endpoint 是否已不匹配最新宿主会话，需要 Workbench 关闭旧连接并自动重连。
    /// </summary>
    /// <param name="sessionId">最新宿主会话标识。</param>
    /// <param name="generation">最新宿主生成代号。</param>
    /// <returns>启用状态且 session 或 generation 已变化时返回 true。</returns>
    public bool RequiresReconnect(string sessionId, long generation)
    {
        return Enabled && (!string.Equals(SessionId, sessionId, StringComparison.Ordinal) || Generation != generation);
    }

    /// <summary>
    /// 从 JSON 文本反序列化 endpoint 描述。
    /// </summary>
    /// <param name="json">endpoint JSON。</param>
    /// <returns>解析后的 endpoint。</returns>
    public static FastChannelEndpoint FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.FastChannelEndpoint)
            ?? Disabled(string.Empty, string.Empty, 0L);
    }

    /// <summary>
    /// 将 endpoint 描述序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.FastChannelEndpoint);
    }

    /// <summary>
    /// 创建启用状态 endpoint，并统一填充 FileBridge fallback。
    /// </summary>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生成代号。</param>
    /// <param name="transport">传输类型。</param>
    /// <param name="endpoint">传输端点。</param>
    /// <returns>启用状态的 endpoint 描述。</returns>
    private static FastChannelEndpoint CreateEnabled(
        string engineId,
        string sessionId,
        long generation,
        string transport,
        string endpoint)
    {
        return new FastChannelEndpoint
        {
            EngineId = engineId,
            SessionId = sessionId,
            Generation = generation,
            Transport = transport,
            Endpoint = endpoint,
            Enabled = true,
            Fallback = FILEBRIDGE_FALLBACK,
            ReadOnlyCommands = new List<string>
            {
                YokiFrameFastChannelContract.CreateCommandKey("System", "ping"),
                YokiFrameFastChannelContract.CreateCommandKey("System", "bridge_status")
            }
        };
    }
}
