using System.Diagnostics;
using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 CLI FastChannel 状态入口，确保 AI 和 Workbench 能共用同一观测语义。
/// </summary>
public sealed class CliFastChannelStatusTests
{
    /// <summary>
    /// 验证 CLI 可以从 engine registry 读取 Named Pipe endpoint，并明确 FileBridge fallback。
    /// </summary>
    [Fact]
    public async Task FastChannelStatusReadsNamedPipeEndpoint()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        projectRoot.WriteEngineRegistry(
            "\"fastChannels\":[{\"protocolVersion\":1,\"engineId\":\"unity-editor\",\"sessionId\":\"session-a\",\"generation\":9,\"transport\":\"namedPipe\",\"endpoint\":\"YokiFrame.FastChannel.unity-editor\",\"enabled\":true,\"fallback\":\"filebridge\"}]");

        var result = await RunCliAsync("fastchannel", "status", "--project", projectRoot.Path, "--engine", "unity-editor");
        JsonNode json = AssertSuccess(result);
        JsonNode endpoint = json["endpoint"]!;

        Assert.Equal("fastchannel status", json["command"]!.GetValue<string>());
        Assert.Equal("engineRegistry", json["source"]!.GetValue<string>());
        Assert.True(endpoint["enabled"]!.GetValue<bool>());
        Assert.Equal("namedPipe", endpoint["transport"]!.GetValue<string>());
        Assert.Equal("YokiFrame.FastChannel.unity-editor", endpoint["endpoint"]!.GetValue<string>());
        Assert.Equal("filebridge", endpoint["fallback"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 registry 未声明 FastChannel 时 CLI 仍输出 disabled endpoint，调用侧可以直接回落 FileBridge。
    /// </summary>
    [Fact]
    public async Task FastChannelStatusFallsBackWhenEndpointIsMissing()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        projectRoot.WriteEngineRegistry(string.Empty);

        var result = await RunCliAsync("fastchannel", "status", "--project", projectRoot.Path, "--engine", "unity-editor");
        JsonNode json = AssertSuccess(result);
        JsonNode endpoint = json["endpoint"]!;

        Assert.Equal("fallback", json["source"]!.GetValue<string>());
        Assert.False(endpoint["enabled"]!.GetValue<bool>());
        Assert.Equal("none", endpoint["transport"]!.GetValue<string>());
        Assert.Equal("filebridge", endpoint["fallback"]!.GetValue<string>());
    }

    /// <summary>
    /// 启动真实 CLI 程序并捕获标准输出和标准错误。
    /// </summary>
    /// <param name="arguments">传递给 CLI 的参数列表。</param>
    /// <returns>进程退出结果。</returns>
    private static Task<CliProcessResult> RunCliAsync(params string[] arguments)
        => CliTestHelpers.RunCliAsync(arguments);

    /// <summary>
    /// 断言 CLI 成功输出 compact JSON。
    /// </summary>
    /// <param name="result">CLI 执行结果。</param>
    /// <returns>解析后的 JSON。</returns>
    private static JsonNode AssertSuccess(CliProcessResult result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError.Trim());

        var json = JsonNode.Parse(result.StandardOutput)
            ?? throw new InvalidOperationException("CLI stdout is not JSON.");
        Assert.True(json["ok"]!.GetValue<bool>());
        return json;
    }

    /// <summary>
    /// 根据测试输出目录定位同一 solution 构建出的 CLI 程序。
    /// </summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
        => CliTestHelpers.GetCliAssemblyPath();

    /// <summary>
    /// 为 FastChannel CLI 测试创建最小 engine registry 项目根。
    /// </summary>
    private sealed class CliTestProjectRoot : IDisposable
    {
        private const string ENGINE_ID = "unity-editor";

        /// <summary>
        /// 创建临时项目根目录和 engine registry 目录。
        /// </summary>
        private CliTestProjectRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yokiframe-cli-fastchannel-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID));
        }

        /// <summary>
        /// 获取临时项目根路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 创建新的临时项目根。
        /// </summary>
        /// <returns>临时项目根实例。</returns>
        public static CliTestProjectRoot Create()
        {
            return new CliTestProjectRoot();
        }

        /// <summary>
        /// 写入测试用 engine registry。
        /// </summary>
        /// <param name="extraJsonFields">追加到 registry 根对象的 JSON 字段，传空字符串表示没有额外字段。</param>
        public void WriteEngineRegistry(string extraJsonFields)
        {
            var suffix = string.IsNullOrWhiteSpace(extraJsonFields) ? string.Empty : "," + extraJsonFields;
            File.WriteAllText(
                System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"sessionId\":\"session-a\",\"generation\":9" + suffix + "}");
        }

        /// <summary>
        /// 清理测试创建的临时项目根目录。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
