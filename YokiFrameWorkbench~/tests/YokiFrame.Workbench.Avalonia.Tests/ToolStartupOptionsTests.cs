using YokiFrame.Workbench.Avalonia;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖单一 Avalonia 工具应用的启动模式选择。
/// </summary>
public sealed class ToolStartupOptionsTests
{
    /// <summary>
    /// 验证引擎侧传入项目根时启动 Workbench 模式。
    /// </summary>
    [Fact]
    public void FromArgsSelectsWorkbenchModeWhenProjectIsProvided()
    {
        var projectRoot = CreateTempRoot("project");
        var options = ToolStartupOptions.FromArgs(new[] { "--project", projectRoot }, Directory.GetCurrentDirectory(), Directory.GetCurrentDirectory());

        Assert.Equal(ToolStartupMode.Workbench, options.Mode);
        Assert.Equal(Path.GetFullPath(projectRoot), options.ProjectRoot);
    }

    /// <summary>
    /// 验证 Workbench 显式 source 优先于运行目录探测结果，支持宿主传入真实包根。
    /// </summary>
    [Fact]
    public void FromArgsWorkbenchPrefersExplicitSourcePackageRoot()
    {
        var projectRoot = CreateTempRoot("workbench-explicit-project");
        var explicitPackageRoot = CreatePackageRoot(CreateTempRoot("workbench-explicit-source"));
        var detectedPackageRoot = CreatePackageRoot(CreateTempRoot("workbench-detected-source"));
        var appBaseDirectory = Path.Combine(detectedPackageRoot, "development-host", "win-x64");
        Directory.CreateDirectory(appBaseDirectory);

        var options = ToolStartupOptions.FromArgs(
            new[] { "--project", projectRoot, "--source", explicitPackageRoot },
            appBaseDirectory,
            appBaseDirectory);

        Assert.Equal(Path.GetFullPath(explicitPackageRoot), options.SourcePackageRoot);
    }

    /// <summary>
    /// 验证 Workbench 可从应用目录识别 Godot 或 PackageCache 中的真实包根，而不是强制重拼 Assets/YokiFrame。
    /// </summary>
    [Fact]
    public void FromArgsWorkbenchDetectsPortablePackageRootBeforeUnityFallback()
    {
        var projectRoot = CreateTempRoot("workbench-portable-project");
        var packageRoot = CreatePackageRoot(Path.Combine(
            projectRoot,
            "addons",
            "yokiframe",
            "package",
            "YokiFrame"));
        var appBaseDirectory = Path.Combine(packageRoot, "development-host", "win-x64");
        Directory.CreateDirectory(appBaseDirectory);

        var options = ToolStartupOptions.FromArgs(
            new[] { "--project", projectRoot },
            projectRoot,
            appBaseDirectory);

        Assert.Equal(Path.GetFullPath(packageRoot), options.SourcePackageRoot);
    }

    /// <summary>
    /// 验证引擎侧传入父窗口句柄时，Workbench 能保存该句柄供平台层挂载窗口。
    /// </summary>
    [Fact]
    public void FromArgsParsesDecimalParentWindowHandle()
    {
        var projectRoot = CreateTempRoot("project");
        var options = ToolStartupOptions.FromArgs(
            new[] { "--project", projectRoot, "--parent-hwnd", "4660" },
            Directory.GetCurrentDirectory(),
            Directory.GetCurrentDirectory());

        Assert.Equal(4660, options.ParentWindowHandle.ToInt64());
    }

    /// <summary>
    /// 验证父窗口句柄支持十六进制文本，便于宿主直接传递诊断日志中的 HWND。
    /// </summary>
    [Fact]
    public void FromArgsParsesHexParentWindowHandle()
    {
        var projectRoot = CreateTempRoot("project");
        var options = ToolStartupOptions.FromArgs(
            new[] { "--project", projectRoot, "--parent-hwnd=0x1234" },
            Directory.GetCurrentDirectory(),
            Directory.GetCurrentDirectory());

        Assert.Equal(4660, options.ParentWindowHandle.ToInt64());
    }

    /// <summary>
    /// 验证直接从源码包根启动 Installer 时回推源包和 Unity 项目路径。
    /// </summary>
    [Fact]
    public void FromArgsSelectsInstallerModeWhenOpenedFromSourcePackageRoot()
    {
        var projectRoot = CreateTempRoot("project");
        var packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
        Directory.CreateDirectory(Path.Combine(packageRoot, "Documentation~"));
        File.WriteAllText(Path.Combine(packageRoot, "package.json"), "{\"name\":\"com.hinatayoki.yokiframe\"}");

        var options = ToolStartupOptions.FromArgs(Array.Empty<string>(), packageRoot, packageRoot);

        Assert.Equal(ToolStartupMode.Installer, options.Mode);
        Assert.Equal(Path.Combine(projectRoot, "Assets", "YokiFrame"), options.SourcePackageRoot);
        Assert.Equal(projectRoot, options.TargetProjectRoot);
    }

    /// <summary>
    /// 验证复制后的启动器可从有限层级的祖先目录发现同级 Assets/YokiFrame 包根，避免只能从 Runtime 原位置启动。
    /// </summary>
    [Fact]
    public void FromArgsFindsPackageRootFromPortableLauncherAncestor()
    {
        var projectRoot = CreateTempRoot("portable-launcher");
        var packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
        var appBaseDirectory = Path.Combine(projectRoot, "launcher", "bin", "win-x64");
        Directory.CreateDirectory(Path.Combine(packageRoot, "Documentation~"));
        Directory.CreateDirectory(appBaseDirectory);
        File.WriteAllText(Path.Combine(packageRoot, "package.json"), "{\"name\":\"com.hinatayoki.yokiframe\"}");

        var options = ToolStartupOptions.FromArgs(Array.Empty<string>(), appBaseDirectory, appBaseDirectory);

        Assert.Equal(ToolStartupMode.Installer, options.Mode);
        Assert.Equal(Path.GetFullPath(packageRoot), options.SourcePackageRoot);
        Assert.Equal(Path.GetFullPath(projectRoot), options.TargetProjectRoot);
    }

    /// <summary>
    /// 验证 Installer 模式支持手动覆盖 source 和 target 路径。
    /// </summary>
    [Fact]
    public void FromArgsUsesInstallerSourceAndTargetOverrides()
    {
        var sourceRoot = CreateTempRoot("source");
        var targetRoot = CreateTempRoot("target");

        var options = ToolStartupOptions.FromArgs(new[] { "--source", sourceRoot, "--target", targetRoot }, Directory.GetCurrentDirectory(), Directory.GetCurrentDirectory());

        Assert.Equal(ToolStartupMode.Installer, options.Mode);
        Assert.Equal(Path.GetFullPath(sourceRoot), options.SourcePackageRoot);
        Assert.Equal(Path.GetFullPath(targetRoot), options.TargetProjectRoot);
    }

    /// <summary>
    /// 创建测试专用临时目录。
    /// </summary>
    /// <param name="name">目录名片段。</param>
    /// <returns>临时目录完整路径。</returns>
    private static string CreateTempRoot(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-tool-startup-tests", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// 创建满足包根探测约束的最小 YokiFrame 目录。
    /// </summary>
    /// <param name="root">待初始化的包根。</param>
    /// <returns>包根完整路径。</returns>
    private static string CreatePackageRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        Directory.CreateDirectory(Path.Combine(fullRoot, "Documentation~"));
        File.WriteAllText(Path.Combine(fullRoot, "package.json"), "{\"name\":\"com.hinatayoki.yokiframe\"}");
        return fullRoot;
    }
}
