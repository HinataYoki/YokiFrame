using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Protocol.FileBridge;

/// <summary>
/// 表示 `.yokiframe/engines/&lt;engineId&gt;/engine.json` 中的 engine registry 条目。
/// </summary>
public sealed class EngineRegistryEntry
{
    private List<string> mCapabilities = new();
    private List<FastChannelEndpoint> mFastChannels = new();
    private Dictionary<string, JsonElement> mExtensionData = new();

    /// <summary>
    /// 获取或设置协议版本。
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// 获取或设置 engine 标识。
    /// </summary>
    [JsonPropertyName("engineId")]
    public string EngineId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 engine 类型，例如 Unity 或 Godot。
    /// </summary>
    [JsonPropertyName("engine")]
    public string Engine { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 engine 或宿主版本。
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置项目根路径。
    /// </summary>
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 adapter 版本。
    /// </summary>
    [JsonPropertyName("adapterVersion")]
    public string AdapterVersion { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置当前宿主会话标识；会话改变后 Client 必须丢弃旧 FastChannel 连接。
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置当前宿主生成代号；生命周期重建后 Client 必须按新 generation 重连或回落。
    /// </summary>
    [JsonPropertyName("generation")]
    public long Generation { get; set; }

    /// <summary>
    /// 获取或设置宿主当前模式，例如 Edit、Play 或 Runtime。
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 engine 启动时间。
    /// </summary>
    [JsonPropertyName("startedAtUtc")]
    public string StartedAtUtc { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 capability 列表。
    /// </summary>
    [JsonPropertyName("capabilities")]
    public List<string> Capabilities
    {
        get => mCapabilities;
        set => mCapabilities = value ?? new();
    }

    /// <summary>
    /// 获取或设置宿主发布的 FastChannel endpoint 列表；空列表表示只能使用 FileBridge fallback。
    /// </summary>
    [JsonPropertyName("fastChannels")]
    public List<FastChannelEndpoint> FastChannels
    {
        get => mFastChannels;
        set => mFastChannels = value ?? new();
    }

    /// <summary>
    /// 获取或设置未被当前 SDK 显式建模的字段，保证 roundtrip 不丢失。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData
    {
        get => mExtensionData;
        set => mExtensionData = value ?? new Dictionary<string, JsonElement>();
    }

    /// <summary>
    /// 从 JSON 文本反序列化 engine registry 条目。
    /// </summary>
    /// <param name="json">engine.json 内容。</param>
    /// <returns>解析后的 registry 条目。</returns>
    public static EngineRegistryEntry FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.EngineRegistryEntry)
            ?? throw new JsonException("Engine registry JSON must contain an object, not null.");
    }

    /// <summary>
    /// 将 registry 条目序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.EngineRegistryEntry);
    }
}
