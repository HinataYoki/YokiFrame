using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 WorkbenchDoctorService 对 FileBridge 诊断状态的聚合。
/// </summary>
public sealed class WorkbenchDoctorServiceTests
{
    /// <summary>
    /// 验证 heartbeat 新鲜且没有 deadletter 时 doctor 返回 Healthy。
    /// </summary>
    [Fact]
    public void AnalyzeReportsHealthyWhenHeartbeatIsFresh()
    {
        using var projectRoot = DoctorTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow);

        var report = new WorkbenchDoctorService(projectRoot.Path).Analyze("unity-editor");

        Assert.Equal("unity-editor", report.EngineId);
        Assert.Equal("Healthy", report.Level);
        Assert.Equal(0, report.IssueCount);
        Assert.Empty(report.Issues);
        Assert.NotNull(report.Status);
    }

    /// <summary>
    /// 验证 heartbeat 缺失时 doctor 返回 Warning，并提供 heartbeat 证据路径。
    /// </summary>
    [Fact]
    public void AnalyzeReportsWarningWhenHeartbeatIsMissing()
    {
        using var projectRoot = DoctorTestProjectRoot.Create();

        var report = new WorkbenchDoctorService(projectRoot.Path).Analyze("unity-editor");

        Assert.Equal("Warning", report.Level);
        Assert.Equal(1, report.IssueCount);
        Assert.Equal("HeartbeatMissing", report.Issues[0].Code);
        Assert.Contains(projectRoot.HeartbeatPath, report.Issues[0].EvidencePaths);
    }

    /// <summary>
    /// 验证 heartbeat 过期时 doctor 返回 Warning，并指向 heartbeat 文件。
    /// </summary>
    [Fact]
    public void AnalyzeReportsWarningWhenHeartbeatIsStale()
    {
        using var projectRoot = DoctorTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow.AddMinutes(-5));

        var report = new WorkbenchDoctorService(projectRoot.Path).Analyze("unity-editor");

        Assert.Equal("Warning", report.Level);
        Assert.Equal(1, report.IssueCount);
        Assert.Equal("HeartbeatStale", report.Issues[0].Code);
        Assert.Contains(projectRoot.HeartbeatPath, report.Issues[0].EvidencePaths);
    }

    /// <summary>
    /// 验证 registry 已切换到新宿主身份而 heartbeat 仍为旧身份时，doctor 输出可供 CLI 和 AI 使用的明确诊断。
    /// </summary>
    [Fact]
    public void AnalyzeReportsHostIdentityMismatchWhenRegistryAndHeartbeatGenerationsDiffer()
    {
        using var projectRoot = DoctorTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow, "old-session", 7L);
        projectRoot.WriteEngineRegistry("current-session", 8L);

        var report = new WorkbenchDoctorService(projectRoot.Path).Analyze("unity-editor");

        Assert.Equal("Warning", report.Level);
        Assert.Equal(1, report.IssueCount);
        Assert.Equal("HostIdentityMismatch", report.Issues[0].Code);
        Assert.Contains(projectRoot.HeartbeatPath, report.Issues[0].EvidencePaths);
    }

    /// <summary>
    /// 验证 deadletter 存在时 doctor 返回 Warning，并保留 deadletter 目录证据。
    /// </summary>
    [Fact]
    public void AnalyzeReportsWarningWhenDeadletterExists()
    {
        using var projectRoot = DoctorTestProjectRoot.Create();
        projectRoot.WriteHeartbeat(DateTimeOffset.UtcNow);
        projectRoot.WriteDeadletter();

        var report = new WorkbenchDoctorService(projectRoot.Path).Analyze("unity-editor");

        Assert.Equal("Warning", report.Level);
        Assert.Equal(1, report.IssueCount);
        Assert.Equal("DeadletterPresent", report.Issues[0].Code);
        Assert.Contains(projectRoot.DeadletterRoot, report.Issues[0].EvidencePaths);
    }

    /// <summary>
    /// 创建 doctor 测试用最小 FileBridge 项目根。
    /// </summary>
    private sealed class DoctorTestProjectRoot : IDisposable
    {
        private const string ENGINE_ID = "unity-editor";

        /// <summary>
        /// 初始化临时项目根和最小 FileBridge 目录。
        /// </summary>
        private DoctorTestProjectRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yokiframe-workbench-doctor-tests", Guid.NewGuid().ToString("N"));
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
        /// 创建新的测试项目根。
        /// </summary>
        /// <returns>测试项目根。</returns>
        public static DoctorTestProjectRoot Create()
        {
            return new DoctorTestProjectRoot();
        }

        /// <summary>
        /// 写入指定时间的 heartbeat。
        /// </summary>
        /// <param name="createdAtUtc">heartbeat 创建时间。</param>
        /// <param name="sessionId">写入的宿主会话标识。</param>
        /// <param name="generation">写入的宿主 generation。</param>
        public void WriteHeartbeat(DateTimeOffset createdAtUtc, string sessionId = "test", long generation = 1L)
        {
            File.WriteAllText(
                HeartbeatPath,
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"" + sessionId + "\",\"generation\":" + generation + ",\"mode\":\"EditMode\",\"sequence\":1,\"createdAtUtc\":\""
                + createdAtUtc.ToUniversalTime().ToString("O")
                + "\"}");
        }

        /// <summary>
        /// 写入最小 engine registry，使 doctor 可以读取与 heartbeat 对比的当前宿主身份。
        /// </summary>
        /// <param name="sessionId">写入的宿主会话标识。</param>
        /// <param name="generation">写入的宿主 generation。</param>
        public void WriteEngineRegistry(string sessionId, long generation)
        {
            var engineRoot = System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID);
            File.WriteAllText(
                System.IO.Path.Combine(engineRoot, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"version\":\"test\",\"projectPath\":\""
                + Escape(Path)
                + "\",\"adapterVersion\":\"test\",\"sessionId\":\""
                + sessionId
                + "\",\"generation\":"
                + generation
                + ",\"mode\":\"EditMode\",\"capabilities\":[]}");
        }

        /// <summary>
        /// 写入最小 deadletter 证据文件。
        /// </summary>
        public void WriteDeadletter()
        {
            Directory.CreateDirectory(DeadletterRoot);
            File.WriteAllText(System.IO.Path.Combine(DeadletterRoot, "failed-command.json"), "{}");
        }

        /// <summary>
        /// 清理测试项目根。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }

        /// <summary>
        /// 转义 Windows 路径中的反斜杠，避免测试 registry JSON 无效。
        /// </summary>
        /// <param name="text">待转义的路径文本。</param>
        /// <returns>JSON 字符串中可用的路径文本。</returns>
        private static string Escape(string text)
        {
            return text.Replace("\\", "\\\\", StringComparison.Ordinal);
        }
    }
}
