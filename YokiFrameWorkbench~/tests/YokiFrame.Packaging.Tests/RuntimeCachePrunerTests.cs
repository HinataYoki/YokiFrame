using YokiFrame.RuntimeCache;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Runtime 成功发布后的旧源码指纹缓存回收边界。
/// </summary>
public sealed class RuntimeCachePrunerTests
{
    private const string CURRENT_FINGERPRINT =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OLD_FINGERPRINT =
        "2222222222222222222222222222222222222222222222222222222222222222";

    /// <summary>
    /// 验证只删除旧指纹目录，当前 Runtime、staging 和共享指针均保留。
    /// </summary>
    [Fact]
    public void PruneObsoleteDeletesOnlyOldFingerprintDirectories()
    {
        var projectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-runtime-pruner-tests",
            Guid.NewGuid().ToString("N"));
        var currentRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, CURRENT_FINGERPRINT);
        var oldRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, OLD_FINGERPRINT);
        var cacheRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetCacheRoot(projectRoot);
        Directory.CreateDirectory(currentRoot);
        Directory.CreateDirectory(oldRoot);
        Directory.CreateDirectory(Path.Combine(cacheRoot, ".staging"));
        File.WriteAllText(Path.Combine(cacheRoot, "current.json"), "{}");

        var failures = RuntimeCachePruner.PruneObsolete(projectRoot, CURRENT_FINGERPRINT);

        Assert.Empty(failures);
        Assert.True(Directory.Exists(currentRoot));
        Assert.False(Directory.Exists(oldRoot));
        Assert.True(Directory.Exists(Path.Combine(cacheRoot, ".staging")));
        Assert.True(File.Exists(Path.Combine(cacheRoot, "current.json")));
        Directory.Delete(projectRoot, recursive: true);
    }
}
