using YokiFrame.RuntimeCache;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 验证 Runtime fingerprint lease 会阻止清理正在使用的缓存目录，并在释放后恢复清理能力。
/// </summary>
public sealed class RuntimeCacheLeaseTests
{
    private const string CURRENT_FINGERPRINT = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OLD_FINGERPRINT = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>
    /// 验证活动 lease 保留旧目录，释放后下一轮 prune 可以删除它。
    /// </summary>
    [Fact]
    public void ActiveLeaseDefersPruneUntilReleased()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-runtime-lease-tests", Guid.NewGuid().ToString("N"));
        var currentRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, CURRENT_FINGERPRINT);
        var oldRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, OLD_FINGERPRINT);
        Directory.CreateDirectory(currentRoot);
        Directory.CreateDirectory(oldRoot);

        try
        {
            using (var lease = RuntimeCacheLease.TryAcquire(projectRoot, OLD_FINGERPRINT))
            {
                Assert.NotNull(lease);
                var deferred = RuntimeCachePruner.PruneObsolete(projectRoot, CURRENT_FINGERPRINT);
                Assert.Contains(oldRoot, deferred);
                Assert.True(Directory.Exists(oldRoot));
            }

            var removed = RuntimeCachePruner.PruneObsolete(projectRoot, CURRENT_FINGERPRINT);
            Assert.DoesNotContain(oldRoot, removed);
            Assert.False(Directory.Exists(oldRoot));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }
}
