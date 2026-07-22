using System.Text.Json.Nodes;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// 为 Godot Editor Host 测试提供隔离项目和真实 FileBridge 文件路径。
/// </summary>
internal sealed class GodotEditorFileBridgeHostFixture : IDisposable
{
    /// <summary>
    /// 创建临时 Godot 项目和固定 `godot-editor` 协议路径。
    /// </summary>
    private GodotEditorFileBridgeHostFixture()
    {
        ProjectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-godot-editor-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ProjectRoot);
        File.WriteAllText(Path.Combine(ProjectRoot, "project.godot"), "config_version=5");
        EngineRoot = Path.Combine(ProjectRoot, ".yokiframe", "engines", "godot-editor");
        CommandsRoot = Path.Combine(EngineRoot, "commands");
        ResultsRoot = Path.Combine(EngineRoot, "results");
        RegistryPath = Path.Combine(EngineRoot, "engine.json");
        HeartbeatPath = Path.Combine(EngineRoot, "status", "heartbeat.json");
    }

    /// <summary>获取临时 Godot 项目根。</summary>
    internal string ProjectRoot { get; }

    /// <summary>获取 `godot-editor` engine 协议根。</summary>
    internal string EngineRoot { get; }

    /// <summary>获取命令目录。</summary>
    internal string CommandsRoot { get; }

    /// <summary>获取 terminal response 目录。</summary>
    internal string ResultsRoot { get; }

    /// <summary>获取 engine registry 路径。</summary>
    internal string RegistryPath { get; }

    /// <summary>获取 heartbeat 路径。</summary>
    internal string HeartbeatPath { get; }

    /// <summary>
    /// 创建新的隔离 fixture。
    /// </summary>
    /// <returns>已初始化 fixture。</returns>
    internal static GodotEditorFileBridgeHostFixture Create()
    {
        return new GodotEditorFileBridgeHostFixture();
    }

    /// <summary>
    /// 写入一个符合共享协议的 Editor System 命令。
    /// </summary>
    /// <param name="requestId">安全请求标识。</param>
    /// <param name="action">System action。</param>
    /// <param name="source">命令来源；默认使用 CLI。</param>
    internal void WriteSystemCommand(string requestId, string action, string source = "cli")
    {
        Directory.CreateDirectory(CommandsRoot);
        JsonObject envelope = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = "godot-editor",
            ["source"] = source,
            ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["requestId"] = requestId,
            ["kit"] = "System",
            ["action"] = action,
            ["payloadJson"] = "{}",
            ["timeoutMs"] = 10000
        };
        File.WriteAllText(Path.Combine(CommandsRoot, requestId + ".json"), envelope.ToJsonString());
    }

    /// <summary>
    /// 获取指定请求的 terminal response 路径。
    /// </summary>
    /// <param name="requestId">安全请求标识。</param>
    /// <returns>response 完整路径。</returns>
    internal string GetResponsePath(string requestId)
    {
        return Path.Combine(ResultsRoot, requestId + "-response.json");
    }

    /// <summary>
    /// 读取指定 JSON 文件并要求根节点为对象。
    /// </summary>
    /// <param name="path">JSON 完整路径。</param>
    /// <returns>解析后的 JSON 对象。</returns>
    internal JsonObject ReadObject(string path)
    {
        Assert.True(File.Exists(path), "缺少预期 JSON 文件: " + path);
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException("JSON root is not an object: " + path);
    }

    /// <summary>
    /// 删除测试创建的项目和协议证据。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(ProjectRoot))
        {
            Directory.Delete(ProjectRoot, recursive: true);
        }
    }
}
