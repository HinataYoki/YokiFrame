using YokiFrame;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 验证 `.yokiframe` 清理器只处理白名单终态文件，并按 TTL/数量规则回收旧证据。
/// </summary>
public sealed class ProjectStorageCleanupTests
{
    /// <summary>
    /// 验证过期 archive、deadletter、response 和启动日志会删除，而活动队列与未知文件保留。
    /// </summary>
    [Fact]
    public void PruneDeletesExpiredTerminalEvidenceAndKeepsActiveFiles()
    {
        var projectRoot = CreateProjectRoot();
        var nowUtc = DateTimeOffset.Parse("2026-07-31T12:00:00Z");
        var oldUtc = nowUtc.AddDays(-31);
        try
        {
            var archivePath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "commands", "archive", "old.json");
            var deadletterPath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "commands", "deadletter", "old.json");
            var responsePath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "results", "old-response.json");
            var startupPath = WriteFile(projectRoot, ".yokiframe", "workbench", "startup-old.jsonl");
            var pendingPath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "commands", "pending.json");
            var processingPath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "commands", "processing", "processing.json");
            var unknownResponsePath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "results", "keep.json");
            SetLastWriteTimeUtc(oldUtc, archivePath, deadletterPath, responsePath, startupPath, pendingPath, processingPath, unknownResponsePath);

            var options = new YokiFrameFileBridgeCleanupOptions(
                TimeSpan.FromDays(7), 200,
                TimeSpan.FromDays(7), 200,
                TimeSpan.FromDays(30), 200,
                TimeSpan.FromDays(14), 20,
                TimeSpan.Zero);
            var report = YokiFrameFileBridgePruner.Prune(projectRoot, options, nowUtc);

            Assert.Equal(4, report.DeletedFileCount);
            Assert.False(File.Exists(archivePath));
            Assert.False(File.Exists(deadletterPath));
            Assert.False(File.Exists(responsePath));
            Assert.False(File.Exists(startupPath));
            Assert.True(File.Exists(pendingPath));
            Assert.True(File.Exists(processingPath));
            Assert.True(File.Exists(unknownResponsePath));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证数量上限按最新写入时间保留文件，即使这些文件尚未超过 TTL。
    /// </summary>
    [Fact]
    public void PruneKeepsNewestFilesWhenDirectoryExceedsCountLimit()
    {
        var projectRoot = CreateProjectRoot();
        var nowUtc = DateTimeOffset.Parse("2026-07-31T12:00:00Z");
        try
        {
            var oldestPath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "results", "old-response.json");
            var newestPath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "results", "new-response.json");
            SetLastWriteTimeUtc(nowUtc.AddMinutes(-2), oldestPath);
            SetLastWriteTimeUtc(nowUtc.AddMinutes(-1), newestPath);
            var options = new YokiFrameFileBridgeCleanupOptions(
                TimeSpan.FromDays(7), 200,
                TimeSpan.FromDays(7), 1,
                TimeSpan.FromDays(30), 200,
                TimeSpan.FromDays(14), 20,
                TimeSpan.Zero);

            var report = YokiFrameFileBridgePruner.Prune(projectRoot, options, nowUtc);

            Assert.Equal(1, report.DeletedFileCount);
            Assert.False(File.Exists(oldestPath));
            Assert.True(File.Exists(newestPath));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证清理锁被其它进程持有时本轮跳过且不删除任何证据。
    /// </summary>
    [Fact]
    public void PruneSkipsWhenCleanupLockIsHeld()
    {
        var projectRoot = CreateProjectRoot();
        var archivePath = WriteFile(projectRoot, ".yokiframe", "engines", "unity-editor", "commands", "archive", "old.json");
        var lockPath = Path.Combine(projectRoot, ".yokiframe", "cleanup.lock");
        try
        {
            File.SetLastWriteTimeUtc(archivePath, DateTime.UtcNow.AddDays(-2));
            using (var cleanupLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var report = YokiFrameFileBridgePruner.Prune(
                    projectRoot,
                    new YokiFrameFileBridgeCleanupOptions(
                        TimeSpan.FromDays(1), 1,
                        TimeSpan.FromDays(1), 1,
                        TimeSpan.FromDays(1), 1,
                        TimeSpan.FromDays(1), 1,
                        TimeSpan.Zero));

                Assert.True(report.SkippedDueToLock);
                Assert.Equal(0, report.FailedFileCount);
                Assert.True(File.Exists(archivePath));
            }
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 Runtime 缓存、活动状态和未知文件不属于通用清理白名单。
    /// </summary>
    [Fact]
    public void PruneKeepsRuntimeAndNonWhitelistedStorage()
    {
        var projectRoot = CreateProjectRoot();
        var nowUtc = DateTimeOffset.Parse("2026-07-31T12:00:00Z");
        var runtimePath = WriteFile(
            projectRoot,
            ".yokiframe",
            "runtime",
            "com.hinatayoki.yokiframe",
            "stale-fingerprint",
            "runtime.dll");
        var heartbeatPath = WriteFile(
            projectRoot,
            ".yokiframe",
            "engines",
            "unity-editor",
            "status",
            "heartbeat.json");
        var snapshotPath = WriteFile(
            projectRoot,
            ".yokiframe",
            "engines",
            "unity-editor",
            "snapshots",
            "state.json");
        var unknownResultPath = WriteFile(
            projectRoot,
            ".yokiframe",
            "engines",
            "unity-editor",
            "results",
            "keep.json");
        var unknownWorkbenchPath = WriteFile(
            projectRoot,
            ".yokiframe",
            "workbench",
            "webview2",
            "user-data.bin");
        SetLastWriteTimeUtc(
            nowUtc.AddDays(-365),
            runtimePath,
            heartbeatPath,
            snapshotPath,
            unknownResultPath,
            unknownWorkbenchPath);

        try
        {
            var report = YokiFrameFileBridgePruner.Prune(
                projectRoot,
                new YokiFrameFileBridgeCleanupOptions(
                    TimeSpan.FromDays(1),
                    1,
                    TimeSpan.FromDays(1),
                    1,
                    TimeSpan.FromDays(1),
                    1,
                    TimeSpan.FromDays(1),
                    1,
                    TimeSpan.Zero),
                nowUtc);

            Assert.Equal(0, report.DeletedFileCount);
            Assert.True(File.Exists(runtimePath));
            Assert.True(File.Exists(heartbeatPath));
            Assert.True(File.Exists(snapshotPath));
            Assert.True(File.Exists(unknownResultPath));
            Assert.True(File.Exists(unknownWorkbenchPath));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 创建隔离测试项目根目录。
    /// </summary>
    /// <returns>临时项目根目录。</returns>
    private static string CreateProjectRoot()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-storage-cleanup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        return projectRoot;
    }

    /// <summary>
    /// 创建测试文件并返回完整路径。
    /// </summary>
    /// <param name="projectRoot">测试项目根。</param>
    /// <param name="segments">项目内相对路径片段。</param>
    /// <returns>已写入文件的完整路径。</returns>
    private static string WriteFile(string projectRoot, params string[] segments)
    {
        var path = Path.Combine(new[] { projectRoot }.Concat(segments).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return path;
    }

    /// <summary>
    /// 将多个测试文件设置为同一 UTC 最后写入时间。
    /// </summary>
    /// <param name="timestampUtc">目标 UTC 时间。</param>
    /// <param name="paths">待设置文件路径。</param>
    private static void SetLastWriteTimeUtc(DateTimeOffset timestampUtc, params string[] paths)
    {
        foreach (var path in paths)
        {
            File.SetLastWriteTimeUtc(path, timestampUtc.UtcDateTime);
        }
    }

    /// <summary>
    /// 清理隔离测试项目根目录。
    /// </summary>
    /// <param name="projectRoot">测试项目根。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
