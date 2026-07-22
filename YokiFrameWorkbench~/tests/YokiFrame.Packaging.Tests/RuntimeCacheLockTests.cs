using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖项目 Runtime 包根锁与共享 current.json 的事务边界。
/// </summary>
public sealed class RuntimeCacheLockTests
{
    /// <summary>
    /// 验证任一 fingerprint 发布持有包级锁时，另一 bootstrap 会在建计划和写 current.json 前失败。
    /// </summary>
    [Fact]
    public void BootstrapRejectsConcurrentProjectRuntimePublisherBeforePointerWrite()
    {
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "yokiframe-runtime-lock", Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(fixtureRoot, "source", "YokiFrame");
        var projectRoot = Path.Combine(fixtureRoot, "project");
        Directory.CreateDirectory(Path.Combine(packageRoot, "YokiFrameWorkbench~", "src"));
        Directory.CreateDirectory(projectRoot);
        try
        {
            var cacheRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetCacheRoot(projectRoot);
            using var publishLock = RuntimePublishLock.Acquire(cacheRoot);

            Assert.Throws<IOException>(() =>
                new RuntimeCacheService().Bootstrap(packageRoot, projectRoot, "Release"));

            Assert.True(File.Exists(Path.Combine(cacheRoot, ".publish.lock")));
            Assert.False(File.Exists(YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot)));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }
}
