using YokiFrame.Tooling.Application.Services.Luban;

namespace YokiFrame.Tooling.Application.Tests.Luban;

/// <summary>
/// 覆盖 Luban 预览目录在递归清理前的物理路径边界。
/// </summary>
public sealed class LubanJsonPreviewPathSecurityTests
{
    /// <summary>
    /// 验证项目 Temp 根本身不能作为预览目录，避免一次校验误用清空其它工具的临时文件。
    /// </summary>
    [Fact]
    public void ValidatePreviewDirectoryRejectsProjectTempRoot()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-luban-preview-path-tests", Guid.NewGuid().ToString("N"));
        var tempRoot = Path.Combine(projectRoot, "Temp");

        var exception = Assert.Throws<InvalidDataException>(
            () => LubanJsonPreviewService.ValidatePreviewDirectory(projectRoot, tempRoot));

        Assert.Contains("独占子目录", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证项目根、Temp 或预览子目录任一级是目录链接时都拒绝进入递归清理流程。
    /// </summary>
    /// <param name="linkedComponent">待替换为链接的路径层级。</param>
    [Theory]
    [InlineData("ProjectRoot")]
    [InlineData("Temp")]
    [InlineData("Preview")]
    public void ValidatePreviewDirectoryRejectsReparsePointPathChain(string linkedComponent)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "yokiframe-luban-preview-path-tests", Guid.NewGuid().ToString("N"));
        var realProjectRoot = Path.Combine(testRoot, "real-project");
        var requestedProjectRoot = realProjectRoot;
        var outsideRoot = Path.Combine(testRoot, "outside");
        Directory.CreateDirectory(realProjectRoot);
        Directory.CreateDirectory(outsideRoot);
        var linkPath = CreateScenarioLink(linkedComponent, testRoot, realProjectRoot, outsideRoot, ref requestedProjectRoot);
        try
        {
            if (linkPath == null)
            {
                return;
            }

            var previewPath = Path.Combine(requestedProjectRoot, "Temp", "Preview");

            var exception = Assert.Throws<InvalidDataException>(
                () => LubanJsonPreviewService.ValidatePreviewDirectory(requestedProjectRoot, previewPath));

            Assert.Contains("链接", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteLinkAndRoot(linkPath, testRoot);
        }
    }

    /// <summary>
    /// 为指定层级创建目录链接，并返回链接路径；宿主不支持链接时返回 null。
    /// </summary>
    private static string? CreateScenarioLink(
        string linkedComponent,
        string testRoot,
        string realProjectRoot,
        string outsideRoot,
        ref string requestedProjectRoot)
    {
        var linkPath = linkedComponent switch
        {
            "ProjectRoot" => Path.Combine(testRoot, "linked-project"),
            "Temp" => Path.Combine(realProjectRoot, "Temp"),
            _ => Path.Combine(realProjectRoot, "Temp", "Preview")
        };
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        if (!TryCreateDirectoryLink(linkPath, outsideRoot))
        {
            return null;
        }

        if (linkedComponent == "ProjectRoot")
        {
            requestedProjectRoot = linkPath;
        }

        return linkPath;
    }

    /// <summary>尝试创建目录符号链接；当前宿主不支持或权限不足时跳过专项断言。</summary>
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
    private static void DeleteLinkAndRoot(string? linkPath, string testRoot)
    {
        if (linkPath != null && Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath);
        }

        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
