namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Workbench 构建输入指纹对实际发布资源的响应。
/// </summary>
public sealed class YokiFrameWorkbenchSourceFingerprintTests
{
    /// <summary>
    /// 验证品牌 PNG 内容变化会使源码指纹变化，避免项目缓存继续复用旧图标。
    /// </summary>
    [Fact]
    public void ComputeChangesWhenPublishedPngChanges()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "yokiframe-fingerprint-tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~", "src");
        var pngPath = Path.Combine(sourceRoot, "YokiFrame.Workbench.Avalonia", "Assets", "Brand", "yoki.png");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
            File.WriteAllBytes(pngPath, new byte[] { 1, 2, 3 });
            var before = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);

            File.WriteAllBytes(pngPath, new byte[] { 1, 2, 4 });
            var after = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);

            Assert.NotEqual(before, after);
        }
        finally
        {
            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 验证源码目录中的 reparse point 不会让指纹扫描越过包根读取外部文件。
    /// </summary>
    [Fact]
    public void ComputeDoesNotFollowDirectoryReparsePoints()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "yokiframe-fingerprint-tests", Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(testRoot, "package");
        var sourceRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~", "src");
        var externalRoot = Path.Combine(testRoot, "external");
        var linkPath = Path.Combine(sourceRoot, "external-link");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(externalRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "inside.cs"), "internal sealed class Inside {}\n");
        var externalPath = Path.Combine(externalRoot, "outside.cs");
        File.WriteAllText(externalPath, "internal sealed class Outside {}\n");

        try
        {
            if (!TryCreateDirectoryLink(linkPath, externalRoot))
            {
                return;
            }

            var before = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);
            File.WriteAllText(externalPath, "internal sealed class ChangedOutside {}\n");
            var after = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);

            Assert.Equal(before, after);
        }
        finally
        {
            DeleteFixture(testRoot, linkPath);
        }
    }

    /// <summary>
    /// 验证 package、Workbench 或 src 枚举根自身是目录链接时不会跟随到包外计算可信指纹。
    /// </summary>
    /// <param name="linkedRoot">待替换为链接的根层级。</param>
    [Theory]
    [InlineData("package")]
    [InlineData("workbench")]
    [InlineData("source")]
    public void ComputeRejectsReparsePointInputRoot(string linkedRoot)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "yokiframe-fingerprint-root-tests", Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(testRoot, "package");
        var workbenchRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~");
        var sourceRoot = Path.Combine(workbenchRoot, "src");
        var externalRoot = Path.Combine(testRoot, "external", linkedRoot);
        var linkPath = linkedRoot == "package"
            ? packageRoot
            : linkedRoot == "workbench" ? workbenchRoot : sourceRoot;
        var targetSourceRoot = linkedRoot switch
        {
            "package" => Path.Combine(externalRoot, "YokiFrameWorkbench~", "src"),
            "workbench" => Path.Combine(externalRoot, "src"),
            _ => externalRoot
        };
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        Directory.CreateDirectory(targetSourceRoot);
        File.WriteAllText(Path.Combine(targetSourceRoot, "Outside.cs"), "internal sealed class Outside {}\n");
        try
        {
            if (!TryCreateDirectoryLink(linkPath, externalRoot))
            {
                return;
            }

            var exception = Assert.Throws<InvalidDataException>(
                () => YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot));

            Assert.Contains("symbolic link or junction", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteFixture(testRoot, linkPath);
        }
    }

    /// <summary>
    /// 验证 `bin` 和 `obj` 中的大量生成文件不会进入指纹或影响后台检测结果。
    /// </summary>
    [Fact]
    public void ComputeIgnoresGeneratedBuildDirectories()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "yokiframe-fingerprint-tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~", "src");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Source.cs"), "internal sealed class Source {}\n");
        var before = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);

        Directory.CreateDirectory(Path.Combine(sourceRoot, "Project", "obj"));
        Directory.CreateDirectory(Path.Combine(sourceRoot, "Project", "bin"));
        File.WriteAllText(Path.Combine(sourceRoot, "Project", "obj", "Generated.cs"), "generated-a");
        File.WriteAllText(Path.Combine(sourceRoot, "Project", "bin", "Generated.json"), "generated-b");
        var after = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);

        Assert.Equal(before, after);
        Directory.Delete(packageRoot, recursive: true);
    }

    /// <summary>
    /// 验证已取消的窗口生命周期会立即终止源码指纹扫描。
    /// </summary>
    [Fact]
    public void ComputeHonorsCancellationToken()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "yokiframe-fingerprint-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(packageRoot, "YokiFrameWorkbench~", "src"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot, cancellation.Token));

        Directory.Delete(packageRoot, recursive: true);
    }

    /// <summary>
    /// 尝试创建目录符号链接；当前测试环境不允许链接时跳过该平台断言。
    /// </summary>
    /// <param name="linkPath">链接路径。</param>
    /// <param name="targetPath">链接目标。</param>
    /// <returns>创建成功时返回 true。</returns>
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

    /// <summary>
    /// 先移除目录链接，再删除隔离测试根，避免递归清理触达链接目标。
    /// </summary>
    /// <param name="testRoot">隔离测试根。</param>
    /// <param name="linkPath">可能已创建的目录链接。</param>
    private static void DeleteFixture(string testRoot, string linkPath)
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
