namespace YokiFrame.Client.Tests.FileBridge;

/// <summary>
/// 覆盖统一 Client 对 heartbeat 文件的缺失和兼容解析行为。
/// </summary>
public sealed class YokiFrameClientHeartbeatTests
{
    /// <summary>
    /// 验证 heartbeat 文件尚未发布时返回 null，不把宿主离线视为解析异常。
    /// </summary>
    [Fact]
    public void MissingHeartbeatReturnsNull()
    {
        var client = new YokiFrameClient(CreateProjectRoot());

        var heartbeat = client.ReadHeartbeat("unity-editor");

        Assert.Null(heartbeat);
    }

    /// <summary>
    /// 验证真实 heartbeat 文件会保留解析后的会话字段与本机证据路径。
    /// </summary>
    [Fact]
    public void ReadHeartbeatReturnsResolvedEvidencePathAndParsedContract()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var client = new YokiFrameClient(projectRoot);
            var heartbeatPath = client.Paths.GetHeartbeatPath("unity-editor");
            Directory.CreateDirectory(Path.GetDirectoryName(heartbeatPath)!);
            File.WriteAllText(
                heartbeatPath,
                "{\"engineId\":\"unity-editor\",\"createdAtUtc\":\"2026-07-10T00:00:00Z\",\"sessionId\":\"session-a\",\"generation\":7}");

            var heartbeat = client.ReadHeartbeat("unity-editor");

            Assert.NotNull(heartbeat);
            Assert.Equal(heartbeatPath, heartbeat.Path);
            Assert.Equal("session-a", heartbeat.SessionId);
            Assert.Equal(7, heartbeat.Generation);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 创建唯一测试项目根目录。
    /// </summary>
    /// <returns>测试项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-heartbeat-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 清理测试创建的项目目录；目录未创建时不执行操作。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, true);
        }
    }
}
