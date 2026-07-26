using System.Diagnostics;
using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 CLI doctor 首切片，确保诊断输出能被脚本和 Workbench 稳定解析。
/// </summary>
public sealed class CliDoctorTests
{
    /// <summary>
    /// 验证 heartbeat 新鲜且队列为空时 doctor 输出 Healthy。
    /// </summary>
    [Fact]
    public async Task DoctorReportsHealthyWhenBridgeHeartbeatIsFresh()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow);

        var result = await RunCliAsync(
            "doctor",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor");

        JsonNode json = AssertSuccess(result);
        Assert.Equal("doctor", json["command"]!.GetValue<string>());
        Assert.Equal("unity-editor", json["engineId"]!.GetValue<string>());
        Assert.Equal("Healthy", json["level"]!.GetValue<string>());
        Assert.Equal(0, json["issueCount"]!.GetValue<int>());
        Assert.Empty(json["issues"]!.AsArray());
    }

    /// <summary>
    /// 验证 heartbeat 过期时 doctor 输出 Warning，并提供 heartbeat 证据路径。
    /// </summary>
    [Fact]
    public async Task DoctorReportsWarningWhenHeartbeatIsStale()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow.AddMinutes(-5));

        var result = await RunCliAsync(
            "doctor",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor");

        JsonNode json = AssertSuccess(result);
        JsonArray issues = json["issues"]!.AsArray();

        Assert.Equal("Warning", json["level"]!.GetValue<string>());
        Assert.Equal(1, json["issueCount"]!.GetValue<int>());
        Assert.Equal("HeartbeatStale", issues[0]!["code"]!.GetValue<string>());
        Assert.Contains(
            projectRoot.HeartbeatPath,
            issues[0]!["evidencePaths"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    /// <summary>
    /// 验证 heartbeat 缺失时 doctor 输出 Warning，并提供 heartbeat 证据路径。
    /// </summary>
    [Fact]
    public async Task DoctorReportsWarningWhenHeartbeatIsMissing()
    {
        using var projectRoot = CliTestProjectRoot.Create();

        var result = await RunCliAsync(
            "doctor",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor");

        JsonNode json = AssertSuccess(result);
        JsonArray issues = json["issues"]!.AsArray();

        Assert.Equal("Warning", json["level"]!.GetValue<string>());
        Assert.Equal(1, json["issueCount"]!.GetValue<int>());
        Assert.Equal("HeartbeatMissing", issues[0]!["code"]!.GetValue<string>());
        Assert.Contains(
            projectRoot.HeartbeatPath,
            issues[0]!["evidencePaths"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    /// <summary>
    /// 验证 deadletter 存在时 doctor 输出 Warning，并保留 deadletter 目录作为证据。
    /// </summary>
    [Fact]
    public async Task DoctorReportsWarningWhenDeadletterExists()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow);
        projectRoot.WriteDeadletter();

        var result = await RunCliAsync(
            "doctor",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor");

        JsonNode json = AssertSuccess(result);
        JsonArray issues = json["issues"]!.AsArray();

        Assert.Equal("Warning", json["level"]!.GetValue<string>());
        Assert.Equal(1, json["issueCount"]!.GetValue<int>());
        Assert.Equal("DeadletterPresent", issues[0]!["code"]!.GetValue<string>());
        Assert.Contains(
            projectRoot.DeadletterRoot,
            issues[0]!["evidencePaths"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    /// <summary>启动真实 CLI 程序并捕获标准输出和标准错误。</summary>
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

    /// <summary>根据测试输出目录定位同一 solution 构建出的 CLI 程序。</summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
        => CliTestHelpers.GetCliAssemblyPath();

    /// <summary>
    /// 为 doctor 测试创建最小 FileBridge 项目根，并在测试结束后清理。
    /// </summary>
    private sealed class CliTestProjectRoot : IDisposable
    {
        private const string ENGINE_ID = "unity-editor";

        /// <summary>
        /// 创建临时项目根目录和最小 FileBridge 目录。
        /// </summary>
        private CliTestProjectRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yokiframe-cli-doctor-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID, "commands"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID, "results"));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(HeartbeatPath)!);
        }

        /// <summary>
        /// 获取临时项目根路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 获取测试 heartbeat 文件路径。
        /// </summary>
        public string HeartbeatPath => System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID, "status", "heartbeat.json");

        /// <summary>
        /// 获取测试 deadletter 目录路径。
        /// </summary>
        public string DeadletterRoot => System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID, "commands", "deadletter");

        /// <summary>
        /// 创建新的临时项目根。
        /// </summary>
        /// <returns>临时项目根实例。</returns>
        public static CliTestProjectRoot Create()
        {
            return new CliTestProjectRoot();
        }

        /// <summary>
        /// 写入指定时间的 heartbeat 文件。
        /// </summary>
        /// <param name="createdAtUtc">heartbeat 创建时间。</param>
        public void WriteHeartbeat(DateTimeOffset createdAtUtc)
        {
            File.WriteAllText(
                HeartbeatPath,
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"test\",\"generation\":1,\"mode\":\"EditMode\",\"sequence\":1,\"createdAtUtc\":\""
                + createdAtUtc.ToUniversalTime().ToString("O")
                + "\"}");
        }

        /// <summary>
        /// 写入一条最小 deadletter 证据。
        /// </summary>
        public void WriteDeadletter()
        {
            Directory.CreateDirectory(DeadletterRoot);
            File.WriteAllText(System.IO.Path.Combine(DeadletterRoot, "failed-command.json"), "{}");
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
