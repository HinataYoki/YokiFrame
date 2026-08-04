using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 FileBridge claim 在并发 worker 下只允许一个请求所有者。
/// </summary>
public sealed class FileBridgeClaimTests
{
    /// <summary>
    /// 验证同一 pending 文件的并发 claim 只有一个成功，并留下唯一 processing 文件。
    /// </summary>
    [Fact]
    public async Task ConcurrentClaimsHaveSingleWinner()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-filebridge-claim-tests",
            Guid.NewGuid().ToString("N"));
        var pendingRoot = Path.Combine(root, "commands");
        var processingRoot = Path.Combine(pendingRoot, "processing");
        var pendingPath = Path.Combine(pendingRoot, "claim-001.json");
        Directory.CreateDirectory(pendingRoot);
        File.WriteAllText(pendingPath, "{}");

        try
        {
            var results = await Task.WhenAll(
                Task.Run(() => YokiFrameFileBridgeClaim.TryClaim(pendingPath, processingRoot, out _)),
                Task.Run(() => YokiFrameFileBridgeClaim.TryClaim(pendingPath, processingRoot, out _)));

            Assert.Single(results, static result => result);
            Assert.Single(Directory.EnumerateFiles(processingRoot, "claim-001.json"));
            Assert.False(File.Exists(pendingPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// 验证 processing 路径无法创建时返回 StorageError，而不是被误判为普通竞争。
    /// </summary>
    [Fact]
    public void ClaimReportsStorageErrorWhenProcessingRootIsAFile()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-filebridge-claim-tests",
            Guid.NewGuid().ToString("N"));
        var pendingRoot = Path.Combine(root, "commands");
        var processingRoot = Path.Combine(root, "processing");
        var pendingPath = Path.Combine(pendingRoot, "storage-error.json");
        Directory.CreateDirectory(pendingRoot);
        File.WriteAllText(pendingPath, "{}");
        File.WriteAllText(processingRoot, "not-a-directory");

        try
        {
            var result = YokiFrameFileBridgeClaim.TryClaim(
                pendingPath,
                processingRoot,
                out var claimedPath,
                out var storageException);

            Assert.Equal(YokiFrameFileBridgeClaimResult.StorageError, result);
            Assert.Equal(string.Empty, claimedPath);
            Assert.NotNull(storageException);
            Assert.True(File.Exists(pendingPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// 验证 processing 命令仍存在时，非空失败证据 marker 不会被 lease 清理掉。
    /// </summary>
    [Fact]
    public void ActiveFailureEvidenceMarkerSurvivesLeaseCleanup()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-filebridge-claim-tests",
            Guid.NewGuid().ToString("N"));
        var processingRoot = Path.Combine(root, "processing");
        var commandPath = Path.Combine(processingRoot, "failure-001.json");
        var markerPath = commandPath + ".claim";
        Directory.CreateDirectory(processingRoot);
        File.WriteAllText(commandPath, "{}");
        File.WriteAllText(markerPath, "deadletter evidence");
        File.SetLastWriteTimeUtc(markerPath, DateTime.UtcNow.AddMinutes(-5));

        try
        {
            YokiFrameFileBridgeClaim.RemoveExpiredMarkers(
                processingRoot,
                DateTime.UtcNow.AddMinutes(-1));

            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// 验证 processing 命令已经离开后，孤儿失败证据 marker 可由同一清理入口回收。
    /// </summary>
    [Fact]
    public void OrphanFailureEvidenceMarkerIsRemovedAfterCommandLeavesProcessing()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-filebridge-claim-tests",
            Guid.NewGuid().ToString("N"));
        var processingRoot = Path.Combine(root, "processing");
        var commandPath = Path.Combine(processingRoot, "failure-002.json");
        var markerPath = commandPath + ".claim";
        Directory.CreateDirectory(processingRoot);
        File.WriteAllText(markerPath, "deadletter evidence");
        File.SetLastWriteTimeUtc(markerPath, DateTime.UtcNow.AddMinutes(-5));

        try
        {
            YokiFrameFileBridgeClaim.RemoveExpiredMarkers(
                processingRoot,
                DateTime.UtcNow.AddMinutes(-1));

            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
