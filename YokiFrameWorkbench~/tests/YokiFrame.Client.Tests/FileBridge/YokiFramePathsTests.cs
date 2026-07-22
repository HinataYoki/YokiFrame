using YokiFrame.Client.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Tests.FileBridge;

/// <summary>
/// 覆盖 FileBridge 路径解析中的 traversal 防护。
/// </summary>
public sealed class YokiFramePathsTests
{
    /// <summary>
    /// 验证相对与绝对项目根会解析到同一个项目内 FileBridge 根。
    /// </summary>
    [Fact]
    public void EquivalentAbsoluteAndRelativeProjectRootsResolveSameFileBridgeRoot()
    {
        var absoluteProjectRoot = Path.Combine(
            Environment.CurrentDirectory,
            ".yokiframe-path-tests",
            Guid.NewGuid().ToString("N"));
        var relativeProjectRoot = Path.GetRelativePath(Environment.CurrentDirectory, absoluteProjectRoot);

        var absolutePaths = new YokiFramePaths(absoluteProjectRoot);
        var relativePaths = new YokiFramePaths(relativeProjectRoot);

        Assert.Equal(absolutePaths.ProjectRoot, relativePaths.ProjectRoot);
        Assert.Equal(absolutePaths.YokiFrameRoot, relativePaths.YokiFrameRoot);
    }

    /// <summary>
    /// 验证 snapshot 路径只允许安全 kit/name，阻止 `..` 逃逸。
    /// </summary>
    [Fact]
    public void SnapshotPathRejectsTraversal()
    {
        var paths = new YokiFramePaths(CreateProjectRoot());
        Assert.Throws<YokiFrameProtocolException>(() => paths.GetSnapshotPath("unity-editor", "../FsmKit", "state"));
        Assert.Throws<YokiFrameProtocolException>(() => paths.GetSnapshotPath("unity-editor", "FsmKit", "../state"));
    }

    /// <summary>
    /// 验证 engine 根自身是目录链接时，客户端不会把 FileBridge 读写重定向到项目外。
    /// </summary>
    [Fact]
    public void EngineRootRejectsReparsePoint()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "yokiframe-path-tests", Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(testRoot, "project");
        var enginesRoot = Path.Combine(projectRoot, ".yokiframe", "engines");
        var engineRoot = Path.Combine(enginesRoot, "unity-editor");
        var outsideRoot = Path.Combine(testRoot, "outside-engine");
        Directory.CreateDirectory(enginesRoot);
        Directory.CreateDirectory(outsideRoot);
        try
        {
            if (!TryCreateDirectoryLink(engineRoot, outsideRoot))
            {
                return;
            }

            var exception = Assert.Throws<YokiFrameProtocolException>(
                () => new YokiFramePaths(projectRoot).GetEngineRoot("unity-editor"));

            Assert.Equal("PathReparsePointRejected", exception.Error.Code);
        }
        finally
        {
            DeleteLinkAndRoot(engineRoot, testRoot);
        }
    }

    /// <summary>
    /// 为测试创建唯一项目根路径，不依赖该目录真实存在。
    /// </summary>
    /// <returns>测试项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-path-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>尝试创建目录符号链接；当前宿主不支持时跳过链接专项断言。</summary>
    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                          or PlatformNotSupportedException
                                          or NotSupportedException
                                          or IOException)
        {
            return false;
        }
    }

    /// <summary>先删除目录链接，再清理隔离测试根。</summary>
    private static void DeleteLinkAndRoot(string linkPath, string testRoot)
    {
        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath);
        }

        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
