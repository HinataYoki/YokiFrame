using System.Text;
using YokiFrame.Json;
using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 通过真实最小 .NET 项目覆盖当前平台发布、staging 提交和失败保护。
/// </summary>
public sealed class RuntimePublishServiceTests : IDisposable
{
    private readonly List<string> mCleanupRoots = new();

    /// <summary>
    /// 验证 Native AOT 发布会生成独立 GUI/CLI profile，并提交可搬运 manifest。
    /// </summary>
    [Fact]
    public void PublishBuildsNativeAotProfileWithCliAndCommitsManifest()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: false);
        var plan = new RuntimePublishPlanBuilder().Build(
            packageRoot,
            CreateRuntimeRoot(packageRoot),
            "Release",
            "win-x64-aot",
            startupOptimized: false);

        var result = new RuntimePublishService().Publish(plan);

        Assert.Equal(plan.Profile.RuntimeIdentifier, result.RuntimeIdentifier);
        Assert.Equal(plan.PublishRoot, result.PublishRoot);
        Assert.True(File.Exists(Path.Combine(plan.PublishRoot, NormalizeEntry(plan.Profile.GuiEntry))));
        Assert.True(File.Exists(Path.Combine(plan.PublishRoot, NormalizeEntry(plan.Profile.CliEntry))));
        Assert.False(Directory.Exists(plan.StagingRoot));
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(plan.ManifestPath));
        var manifestRoot = manifestDocument.RootElement;
        Assert.Equal(".", manifestRoot.GetProperty("runtimeRoot").GetString());
        var platformElement = Assert.Single(manifestRoot.GetProperty("platforms").EnumerateArray());
        Assert.Equal(plan.Profile.RuntimeIdentifier, platformElement.GetProperty("platform").GetString());
        Assert.False(platformElement.GetProperty("sharedRuntime").GetBoolean());
        Assert.Equal("win-x64-aot/yoki.exe", platformElement.GetProperty("cliEntry").GetString());
    }

    /// <summary>
    /// 验证发布只生成 Runtime profile 与 manifest，不会把源码 bootstrap 模板复制到项目缓存。
    /// </summary>
    [Fact]
    public void PublishDoesNotCopySourceBootstrapTemplatesIntoRuntimeCache()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: false);
        var templateRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~", "scripts", "runtime-bootstrap");
        WriteFile(Path.Combine(templateRoot, "build-current-platform.cmd"), "cmd-template");
        WriteFile(Path.Combine(templateRoot, "build-current-platform.sh"), "sh-template");
        WriteFile(Path.Combine(templateRoot, "build-current-platform.command"), "command-template");
        WriteFile(Path.Combine(templateRoot, "install-godot.cmd"), "godot-cmd-template");
        WriteFile(Path.Combine(templateRoot, "install-godot.sh"), "godot-sh-template");
        WriteFile(Path.Combine(templateRoot, "install-godot.command"), "godot-command-template");
        var plan = new RuntimePublishPlanBuilder().BuildCurrent(packageRoot, CreateRuntimeRoot(packageRoot), "Release");

        new RuntimePublishService().Publish(plan);

        Assert.False(File.Exists(Path.Combine(plan.RuntimeRoot, "build-current-platform.cmd")));
        Assert.False(File.Exists(Path.Combine(plan.RuntimeRoot, "build-current-platform.sh")));
        Assert.False(File.Exists(Path.Combine(plan.RuntimeRoot, "build-current-platform.command")));
        Assert.False(File.Exists(Path.Combine(plan.RuntimeRoot, "install-godot.cmd")));
        Assert.False(File.Exists(Path.Combine(plan.RuntimeRoot, "install-godot.sh")));
        Assert.False(File.Exists(Path.Combine(plan.RuntimeRoot, "install-godot.command")));
    }

    /// <summary>
    /// 验证 Runtime 发布不依赖包内 bootstrap 模板；模板仅作为用户手动触发源码 bootstrap 的入口。
    /// </summary>
    [Fact]
    public void PublishDoesNotRequireSourceBootstrapTemplates()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: false);
        var plan = new RuntimePublishPlanBuilder().BuildCurrent(packageRoot, CreateRuntimeRoot(packageRoot), "Release");
        var missingTemplatePath = Path.Combine(
            plan.WorkbenchRoot,
            "scripts",
            "runtime-bootstrap",
            "build-current-platform.command");
        File.Delete(missingTemplatePath);

        var result = new RuntimePublishService().Publish(plan);

        Assert.True(File.Exists(result.GuiPath));
        Assert.True(File.Exists(result.CliPath));
    }

    /// <summary>
    /// 验证 staging 构建失败不会覆盖已有平台目录或 manifest。
    /// </summary>
    [Fact]
    public void PublishFailureLeavesExistingProfileAndManifestUntouched()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: true);
        var plan = new RuntimePublishPlanBuilder().BuildCurrent(packageRoot, CreateRuntimeRoot(packageRoot), "Release");
        var markerPath = Path.Combine(plan.PublishRoot, "existing.marker");
        const string originalManifest = "existing-manifest";
        WriteFile(markerPath, "existing-profile");
        WriteFile(plan.ManifestPath, originalManifest);

        Assert.Throws<InvalidOperationException>(() => new RuntimePublishService().Publish(plan));

        AssertUtf8FileEquals(markerPath, "existing-profile");
        AssertUtf8FileEquals(plan.ManifestPath, originalManifest);
        Assert.False(Directory.Exists(plan.StagingRoot));
    }

    /// <summary>
    /// 验证同一 WorkbenchRuntime 根已有发布进程持锁时，第二个发布在清理或构建前明确失败。
    /// </summary>
    [Fact]
    public void PublishRejectsConcurrentPublisherBeforeWriting()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: false);
        var plan = new RuntimePublishPlanBuilder().BuildCurrent(packageRoot, CreateRuntimeRoot(packageRoot), "Release");
        WriteFile(Path.Combine(plan.PublishRoot, "existing.marker"), "existing-profile");
        using var lockStream = RuntimePublishLock.AcquireForRuntimeRoot(plan.RuntimeRoot);

        Assert.Throws<IOException>(() => new RuntimePublishService().Publish(plan));

        AssertUtf8FileEquals(Path.Combine(plan.PublishRoot, "existing.marker"), "existing-profile");
        Assert.False(Directory.Exists(plan.StagingRoot));
    }

    /// <summary>
    /// 验证上次进程在 profile 切换后、manifest 提交前中断时，下次发布会先恢复旧 profile 与 manifest。
    /// </summary>
    [Fact]
    public void PublishRecoversInterruptedProfileCommitBeforeBuilding()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: true);
        var plan = new RuntimePublishPlanBuilder().BuildCurrent(packageRoot, CreateRuntimeRoot(packageRoot), "Release");
        var transactionRoot = Path.Combine(
            plan.RuntimeRoot,
            ".staging",
            ".transactions",
            plan.Profile.RuntimeIdentifier);
        WriteFile(Path.Combine(plan.PublishRoot, "new.marker"), "uncommitted-profile");
        WriteFile(plan.ManifestPath, "uncommitted-manifest");
        WriteFile(Path.Combine(transactionRoot, "profile.previous", "old.marker"), "stable-profile");
        WriteFile(Path.Combine(transactionRoot, "manifest.previous"), "stable-manifest");
        WriteFile(Path.Combine(transactionRoot, "profile-backed-up.marker"), string.Empty);
        WriteFile(Path.Combine(transactionRoot, "profile-committed.marker"), string.Empty);

        Assert.Throws<InvalidOperationException>(() => new RuntimePublishService().Publish(plan));

        Assert.False(File.Exists(Path.Combine(plan.PublishRoot, "new.marker")));
        AssertUtf8FileEquals(Path.Combine(plan.PublishRoot, "old.marker"), "stable-profile");
        AssertUtf8FileEquals(plan.ManifestPath, "stable-manifest");
        Assert.False(Directory.Exists(transactionRoot));
    }

    /// <summary>
    /// 验证首次发布在 profile 切换开始后中断时，会删除没有配套 manifest 的新目录和索引。
    /// </summary>
    [Fact]
    public void PublishRecoversInterruptedFirstProfileSwitchBeforeBuilding()
    {
        var packageRoot = CreateBuildablePackageRoot(hasCompileError: true);
        var plan = new RuntimePublishPlanBuilder().BuildCurrent(packageRoot, CreateRuntimeRoot(packageRoot), "Release");
        var transactionRoot = Path.Combine(
            plan.RuntimeRoot,
            ".staging",
            ".transactions",
            plan.Profile.RuntimeIdentifier);
        WriteFile(Path.Combine(plan.PublishRoot, "new.marker"), "uncommitted-profile");
        WriteFile(plan.ManifestPath, "uncommitted-manifest");
        WriteFile(Path.Combine(transactionRoot, "profile-missing.marker"), string.Empty);
        WriteFile(Path.Combine(transactionRoot, "manifest-missing.marker"), string.Empty);
        WriteFile(Path.Combine(transactionRoot, "profile-switch-started.marker"), string.Empty);

        Assert.Throws<InvalidOperationException>(() => new RuntimePublishService().Publish(plan));

        Assert.False(Directory.Exists(plan.PublishRoot));
        Assert.False(File.Exists(plan.ManifestPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    /// <summary>
    /// 创建包含两个可执行项目的独立包根；可选注入 CLI 编译错误验证失败保护。
    /// </summary>
    /// <param name="hasCompileError">是否向 CLI 项目写入编译错误。</param>
    /// <returns>测试包根。</returns>
    private string CreateBuildablePackageRoot(bool hasCompileError)
    {
        var packageRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-packaging-tests",
            "publish-package-" + Guid.NewGuid().ToString("N"));
        WriteExecutableProject(packageRoot, "YokiFrame.Workbench.Avalonia", "System.Console.WriteLine(\"gui\");");
        WriteExecutableProject(
            packageRoot,
            "YokiFrame.Cli",
            hasCompileError ? "this is not valid C#;" : "System.Console.WriteLine(\"cli\");");
        var bootstrapRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~", "scripts", "runtime-bootstrap");
        WriteFile(Path.Combine(bootstrapRoot, "build-current-platform.cmd"), "cmd-bootstrap");
        WriteFile(Path.Combine(bootstrapRoot, "build-current-platform.sh"), "sh-bootstrap");
        WriteFile(Path.Combine(bootstrapRoot, "build-current-platform.command"), "command-bootstrap");
        WriteFile(Path.Combine(bootstrapRoot, "install-godot.cmd"), "godot-cmd-bootstrap");
        WriteFile(Path.Combine(bootstrapRoot, "install-godot.sh"), "godot-sh-bootstrap");
        WriteFile(Path.Combine(bootstrapRoot, "install-godot.command"), "godot-command-bootstrap");
        var fullPackageRoot = Path.GetFullPath(packageRoot);
        mCleanupRoots.Add(fullPackageRoot);
        return fullPackageRoot;
    }

    /// <summary>
    /// 为每个临时源码包创建独立的项目级 Runtime 根，确保测试不会把二进制写回源包。
    /// </summary>
    /// <param name="packageRoot">独立源码包根。</param>
    /// <returns>包外的项目缓存 Runtime 根。</returns>
    private string CreateRuntimeRoot(string packageRoot)
    {
        var cacheRoot = Path.Combine(
            Path.GetDirectoryName(packageRoot)!,
            "runtime-cache-" + Guid.NewGuid().ToString("N"));
        mCleanupRoots.Add(cacheRoot);
        return Path.Combine(
            cacheRoot,
            ".yokiframe",
            "runtime",
            "com.hinatayoki.yokiframe",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }

    /// <summary>
    /// 按 UTF-8 字节比较文件副作用，避免用文本读取证明发布结果。
    /// </summary>
    /// <param name="path">待比较文件。</param>
    /// <param name="expected">期望的 UTF-8 文本。</param>
    private static void AssertUtf8FileEquals(string path, string expected)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(path));
    }

    /// <summary>
    /// 删除当前测试创建的源码包和项目 Runtime 缓存。
    /// </summary>
    public void Dispose()
    {
        foreach (var cleanupRoot in mCleanupRoots)
        {
            if (Directory.Exists(cleanupRoot))
            {
                Directory.Delete(cleanupRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 写入无需外部 NuGet 包的最小可执行项目。
    /// </summary>
    /// <param name="packageRoot">测试包根。</param>
    /// <param name="projectName">项目与程序集名。</param>
    /// <param name="programSource">顶层程序源码。</param>
    private static void WriteExecutableProject(string packageRoot, string projectName, string programSource)
    {
        var projectRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~", "src", projectName);
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(
            Path.Combine(projectRoot, projectName + ".csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AssemblyName>"
            + projectName
            + "</AssemblyName></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectRoot, "Program.cs"), programSource);
    }

    /// <summary>
    /// 将 manifest 使用的正斜杠入口转换为当前平台文件路径。
    /// </summary>
    /// <param name="entry">manifest 相对入口。</param>
    /// <returns>当前平台相对路径。</returns>
    private static string NormalizeEntry(string entry)
    {
        return entry.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// 写入测试文件并创建父目录。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <param name="content">文件内容。</param>
    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
