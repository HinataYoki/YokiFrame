using YokiFrame.Packaging;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Runtime bootstrap 调用的 Packaging 命令路由。
/// </summary>
public sealed class PackagingCommandTests
{
    /// <summary>
    /// 验证 `runtime publish-current` 已进入真实命令路由，并对不存在的包根返回明确错误。
    /// </summary>
    [Fact]
    public void RuntimePublishCurrentRejectsMissingPackageRoot()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), "missing-yokiframe-" + Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-project-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        using var error = new StringWriter();
        try
        {
            Directory.CreateDirectory(projectRoot);
            Console.SetError(error);

            var exitCode = Program.Main(new[]
            {
                "runtime",
                "publish-current",
                "--package-root",
                missingRoot,
                "--project-root",
                projectRoot,
                "--configuration",
                "Release"
            });

            Assert.Equal(1, exitCode);
            Assert.Contains("package root", error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(missingRoot, error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 验证 manifest 命令不能把清单写到 WorkbenchRuntime 根外，避免维护参数覆盖任意文件。
    /// </summary>
    [Fact]
    public void ManifestWriteRejectsOutputOutsideRuntimeRoot()
    {
        var runtimeRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-packaging-tests",
            "manifest-command-" + Guid.NewGuid().ToString("N"));
        var entryPath = Path.Combine(runtimeRoot, "win-x64", "Workbench.exe");
        var outsidePath = Path.Combine(Path.GetDirectoryName(runtimeRoot)!, "outside-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
        File.WriteAllText(entryPath, "gui");
        var originalError = Console.Error;
        using var error = new StringWriter();
        try
        {
            Console.SetError(error);
            var exitCode = Program.Main(new[]
            {
                "manifest", "write",
                "--runtime-root", runtimeRoot,
                "--product", "YokiFrameTool",
                "--platform", "win-x64",
                "--gui-entry", "Workbench.exe",
                "--output", outsidePath
            });

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(outsidePath));
            Assert.Contains("manifest", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
