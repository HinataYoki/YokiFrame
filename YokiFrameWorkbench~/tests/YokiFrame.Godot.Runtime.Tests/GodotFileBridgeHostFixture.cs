using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Pipes;
using System.Net.Sockets;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 提供临时 Godot 项目、FileBridge 路径、命令文件和 JSON 断言辅助。
/// </summary>
internal sealed partial class GodotFileBridgeHostFixture : IDisposable
{
    internal const string ENGINE_ID = "godot-runtime";

    /// <summary>
    /// 创建隔离项目根并写入最小 project.godot 证据文件；同时注入内存 Runtime Settings Store，
    /// 避免普通 .NET 测试进程访问未初始化的 Godot 原生 ProjectSettings。
    /// </summary>
    private GodotFileBridgeHostFixture()
    {
        // .NET 测试进程没有已初始化的 Godot 原生单例，必须显式隔离默认设置工厂。
        KitSettings.SetStore(new YokiFrameRuntimeSettingsStore());
        ProjectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-godot-runtime-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ProjectRoot);
        File.WriteAllText(Path.Combine(ProjectRoot, "project.godot"), "config_version=5");
        EngineRoot = Path.Combine(ProjectRoot, ".yokiframe", "engines", ENGINE_ID);
        CommandsRoot = Path.Combine(EngineRoot, "commands");
        ResultsRoot = Path.Combine(EngineRoot, "results");
        ArchiveRoot = Path.Combine(CommandsRoot, "archive");
        DeadletterRoot = Path.Combine(CommandsRoot, "deadletter");
        RegistryPath = Path.Combine(EngineRoot, "engine.json");
        HeartbeatPath = Path.Combine(EngineRoot, "status", "heartbeat.json");
    }

    /// <summary>
    /// 获取临时 Godot 项目根。
    /// </summary>
    internal string ProjectRoot { get; }

    /// <summary>
    /// 获取 godot-runtime engine 协议根。
    /// </summary>
    internal string EngineRoot { get; }

    /// <summary>
    /// 获取命令队列根。
    /// </summary>
    internal string CommandsRoot { get; }

    /// <summary>
    /// 获取结果目录。
    /// </summary>
    internal string ResultsRoot { get; }

    /// <summary>
    /// 获取命令归档目录。
    /// </summary>
    internal string ArchiveRoot { get; }

    /// <summary>
    /// 获取 deadletter 目录。
    /// </summary>
    internal string DeadletterRoot { get; }

    /// <summary>
    /// 获取 engine.json 路径。
    /// </summary>
    internal string RegistryPath { get; }

    /// <summary>
    /// 获取 heartbeat.json 路径。
    /// </summary>
    internal string HeartbeatPath { get; }

    /// <summary>
    /// 创建新的隔离 fixture。
    /// </summary>
    /// <returns>已创建 fixture。</returns>
    internal static GodotFileBridgeHostFixture Create()
    {
        return new GodotFileBridgeHostFixture();
    }

    /// <summary>
    /// 写入一个符合 Runtime FileBridge contract 的命令信封。
    /// </summary>
    /// <param name="requestId">安全请求标识。</param>
    /// <param name="action">System action。</param>
    /// <param name="source">命令来源；默认使用 CLI。</param>
    /// <returns>命令文件完整路径。</returns>
    internal string WriteSystemCommand(string requestId, string action, string source = "cli")
    {
        Directory.CreateDirectory(CommandsRoot);
        var commandPath = Path.Combine(CommandsRoot, requestId + ".json");
        JsonObject envelope = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = ENGINE_ID,
            ["source"] = source,
            ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["requestId"] = requestId,
            ["kit"] = "System",
            ["action"] = action,
            ["payloadJson"] = "{}",
            ["timeoutMs"] = 10000
        };
        File.WriteAllText(commandPath, envelope.ToJsonString());
        return commandPath;
    }

    /// <summary>
    /// 写入无法解析的命令文件，用于验证 deadletter 终态证据。
    /// </summary>
    /// <param name="fileName">命令文件名。</param>
    /// <returns>命令文件完整路径。</returns>
    internal string WriteMalformedCommand(string fileName)
    {
        Directory.CreateDirectory(CommandsRoot);
        var commandPath = Path.Combine(CommandsRoot, fileName);
        File.WriteAllText(commandPath, "{ invalid-json");
        return commandPath;
    }

    /// <summary>
    /// 获取指定请求的 terminal response 路径。
    /// </summary>
    /// <param name="requestId">请求标识。</param>
    /// <returns>response 完整路径。</returns>
    internal string GetResponsePath(string requestId)
    {
        return Path.Combine(ResultsRoot, requestId + "-response.json");
    }

    /// <summary>
    /// 获取指定 Kit 的 state snapshot 路径。
    /// </summary>
    /// <param name="kit">Kit 标识。</param>
    /// <returns>state snapshot 完整路径。</returns>
    internal string GetSnapshotPath(string kit)
    {
        return Path.Combine(EngineRoot, "snapshots", kit, "state.json");
    }

    /// <summary>
    /// 读取 JSON 文件根对象，并在文件缺失或根类型错误时形成明确失败。
    /// </summary>
    /// <param name="path">JSON 文件路径。</param>
    /// <returns>解析后的根对象。</returns>
    internal static JsonObject ReadObject(string path)
    {
        Assert.True(File.Exists(path), "缺少预期 JSON 文件: " + path);
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException("JSON root is not an object: " + path);
    }

    /// <summary>
    /// 读取当前 registry 中唯一的 Godot Runtime FastChannel endpoint，用于真实本机传输测试。
    /// </summary>
    /// <returns>已发布 endpoint JSON 对象。</returns>
    internal JsonObject ReadFastChannelEndpoint()
    {
        var registry = ReadObject(RegistryPath);
        var endpoints = registry["fastChannels"]?.AsArray()
            ?? throw new InvalidDataException("engine.json does not contain fastChannels.");
        return Assert.Single(endpoints)?.AsObject()
            ?? throw new InvalidDataException("FastChannel endpoint is not an object.");
    }

    /// <summary>
    /// 按 registry 声明的本机传输建立测试连接；调用侧负责释放返回的 Stream。
    /// </summary>
    /// <param name="endpoint">engine registry 中的 FastChannel endpoint。</param>
    /// <param name="cancellationToken">连接和读取的测试取消令牌。</param>
    /// <returns>已连接的双向传输流。</returns>
    internal async Task<Stream> ConnectFastChannelAsync(JsonObject endpoint, CancellationToken cancellationToken)
    {
        var transport = endpoint["transport"]?.GetValue<string>() ?? string.Empty;
        var address = endpoint["endpoint"]?.GetValue<string>() ?? string.Empty;
        if (string.Equals(transport, "namedPipe", StringComparison.Ordinal))
        {
            return await ConnectNamedPipeAsync(address, cancellationToken);
        }

        if (string.Equals(transport, "unixDomainSocket", StringComparison.Ordinal))
        {
            return await ConnectUnixDomainSocketAsync(address, cancellationToken);
        }

        throw new InvalidDataException("FastChannel endpoint uses an unsupported test transport: " + transport);
    }

    /// <summary>
    /// 在已连接 FastChannel 上完成当前 Godot Runtime session 的 Hello/HelloAck 校验。
    /// </summary>
    /// <param name="channel">已连接的本机传输流。</param>
    /// <param name="sessionId">当前 Host session。</param>
    /// <param name="generation">当前 Host generation。</param>
    /// <param name="cancellationToken">握手取消令牌。</param>
    /// <returns>握手完成后的异步任务。</returns>
    internal static async Task CompleteFastChannelHandshakeAsync(
        Stream channel,
        string sessionId,
        long generation,
        CancellationToken cancellationToken)
    {
        JsonObject identity = new()
        {
            ["engineId"] = ENGINE_ID,
            ["sessionId"] = sessionId,
            ["generation"] = generation
        };
        await YokiFrameFastChannelFrameStream.WriteAsync(
            channel,
            new YokiFrameFastChannelFrame(
                YokiFrameFastChannelMessageKind.Hello,
                0,
                identity.ToJsonString()),
            cancellationToken);
        var acknowledgement = await YokiFrameFastChannelFrameStream.ReadAsync(channel, cancellationToken);
        Assert.Equal(YokiFrameFastChannelMessageKind.HelloAck, acknowledgement.MessageKind);
    }

    /// <summary>
    /// 创建与 FileBridge 相同字段约束的 System Command frame，供 FastChannel 主线程 dispatcher 测试复用。
    /// </summary>
    /// <param name="requestId">安全请求标识。</param>
    /// <param name="action">System 只读 action。</param>
    /// <returns>可直接写入已握手连接的 Command frame。</returns>
    internal static YokiFrameFastChannelFrame CreateSystemFastChannelCommand(string requestId, string action)
    {
        return CreateFastChannelCommand(requestId, "System", action);
    }

    /// <summary>
    /// 创建与 FileBridge 相同字段约束的任意 Kit Command frame，供 FastChannel 白名单边界测试构造非 System 请求。
    /// </summary>
    /// <param name="requestId">安全请求标识。</param>
    /// <param name="kit">目标 Kit 标识。</param>
    /// <param name="action">目标 action 标识。</param>
    /// <param name="source">命令来源；默认使用 CLI。</param>
    /// <returns>可直接写入已握手连接的 Command frame。</returns>
    internal static YokiFrameFastChannelFrame CreateFastChannelCommand(
        string requestId,
        string kit,
        string action,
        string source = "cli")
    {
        JsonObject command = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = ENGINE_ID,
            ["source"] = source,
            ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["requestId"] = requestId,
            ["kit"] = kit,
            ["action"] = action,
            ["payloadJson"] = "{}",
            ["timeoutMs"] = 1000
        };
        return new YokiFrameFastChannelFrame(
            YokiFrameFastChannelMessageKind.Command,
            0,
            command.ToJsonString());
    }
}
