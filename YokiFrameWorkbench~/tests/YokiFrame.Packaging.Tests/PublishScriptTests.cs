using System.Xml.Linq;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 WorkbenchRuntime 跨平台发布脚本入口，确保各平台复用同一个共享发布逻辑。
/// </summary>
public sealed class PublishScriptTests
{
    /// <summary>
    /// 验证共享发布脚本存在并声明跨平台入口所需参数。
    /// </summary>
    [Fact]
    public void SharedPublishScriptDeclaresCrossPlatformParameters()
    {
        var source = ReadScript("publish-workbenchruntime.ps1");

        Assert.Contains("$RuntimeIdentifier", source, StringComparison.Ordinal);
        Assert.Contains("$ProjectRoot", source, StringComparison.Ordinal);
        Assert.Contains("$StartupOptimized", source, StringComparison.Ordinal);
        Assert.Contains("YokiFrame.Packaging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$GuiEntry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$CliEntry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$MacAppBundleName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolRuntime~", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证共享发布脚本从工具链父目录识别独立包根，并把 bootstrap 同步职责留给 C# 权威实现。
    /// </summary>
    [Fact]
    public void SharedPublishScriptUsesStandalonePackageRootWithoutCopyingBootstrap()
    {
        var source = ReadScript("publish-workbenchruntime.ps1");

        Assert.Contains("$packageRoot = Resolve-Path (Join-Path $workbenchRoot \"..\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-bootstrap", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-RuntimeBootstrapEntries", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Assets\\YokiFrame\\WorkbenchRuntime~", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 PowerShell 只负责参数转发与退出码传播，不再拥有目录删除、dotnet publish 或 manifest 业务。
    /// </summary>
    [Fact]
    public void SharedPublishScriptDelegatesToPackagingAuthority()
    {
        var source = ReadScript("publish-workbenchruntime.ps1");

        Assert.Contains("runtime", source, StringComparison.Ordinal);
        Assert.Contains("publish", source, StringComparison.Ordinal);
        Assert.Contains("--profile", source, StringComparison.Ordinal);
        Assert.Contains("$LASTEXITCODE", source, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet publish", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manifest write", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 C# 发布服务消费 profile 的 CLI、self-contained、ReadyToRun 与 AOT 选项，保持单一实现。
    /// </summary>
    [Fact]
    public void PackagingServiceOwnsProfileSpecificPublishOptions()
    {
        var source = ReadPackagingSource("Services", "RuntimePublishService.cs");

        Assert.Contains("plan.Profile.PublishCli", source, StringComparison.Ordinal);
        Assert.Contains("plan.Profile.SelfContained", source, StringComparison.Ordinal);
        Assert.Contains("plan.Profile.PublishReadyToRun", source, StringComparison.Ordinal);
        Assert.Contains("plan.Profile.PublishAot", source, StringComparison.Ordinal);
        Assert.Contains("-p:PublishReadyToRun=true", source, StringComparison.Ordinal);
        Assert.Contains("-p:YokiFramePublishAot=true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-p:PublishAot=true", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Windows 发布入口复用共享脚本，并产出 exe GUI 和 exe CLI。
    /// </summary>
    [Fact]
    public void WindowsPublishEntryUsesSharedRuntimeScript()
    {
        var source = ReadScript("publish-workbench-win-x64.ps1");

        Assert.Contains("publish-workbenchruntime.ps1", source, StringComparison.Ordinal);
        Assert.Contains("-RuntimeIdentifier \"win-x64\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-GuiEntry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-CliEntry", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Windows 开发菜单调用的发布入口会启用 ReadyToRun，用于降低无预热冷启动时的 JIT 成本。
    /// </summary>
    [Fact]
    public void WindowsPublishEntryEnablesStartupOptimizedReadyToRun()
    {
        var source = ReadScript("publish-workbench-win-x64.ps1");

        Assert.Contains("-StartupOptimized", source, StringComparison.Ordinal);
        Assert.Contains("PublishReadyToRun=true", ReadPackagingSource("Services", "RuntimePublishService.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Windows 发布入口提供 Native AOT 试验路径；发布目录与真实 .NET RID 分离，便于 Ctrl+E 和普通 Release 并行比较。
    /// </summary>
    [Fact]
    public void WindowsPublishEntryProvidesNativeAotExperimentRuntime()
    {
        var windowsSource = ReadScript("publish-workbench-win-x64.ps1");
        var sharedSource = ReadScript("publish-workbenchruntime.ps1");

        Assert.Contains("$NativeAot", windowsSource, StringComparison.Ordinal);
        Assert.Contains("-RuntimeIdentifier \"win-x64-aot\"", windowsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("-DotnetRuntimeIdentifier", windowsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAot=true", sharedSource, StringComparison.Ordinal);
        var serviceSource = ReadPackagingSource("Services", "RuntimePublishService.cs");
        Assert.Contains("-p:YokiFramePublishAot=true", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("-p:PublishAot=true", serviceSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Native AOT 开关只由 GUI 与 CLI 可执行入口映射，依赖项目不会收到 PublishAot。
    /// </summary>
    /// <param name="projectName">可执行入口项目名。</param>
    [Theory]
    [InlineData("YokiFrame.Workbench.Avalonia")]
    [InlineData("YokiFrame.Cli")]
    public void NativeAotSwitchIsMappedByExecutableEntrypoints(string projectName)
    {
        XDocument project = ReadSourceProject(projectName);
        XElement publishAot = Assert.Single(project.Descendants("PublishAot"));

        Assert.Equal("true", publishAot.Value);
        Assert.Equal("'$(YokiFramePublishAot)' == 'true'", (string?)publishAot.Attribute("Condition"));
    }

    /// <summary>
    /// 验证承载 TableKit 生成器的 Application 项目不映射 Native AOT 开关。
    /// </summary>
    [Fact]
    public void PortableToolingProjectDoesNotMapNativeAotSwitch()
    {
        XDocument project = ReadSourceProject("YokiFrame.Tooling.Application");

        Assert.Empty(project.Descendants("PublishAot"));
    }

    /// <summary>
    /// 验证 macOS 发布入口使用 .app 内部可执行文件作为 GUI 入口，并保留无扩展名 CLI。
    /// </summary>
    /// <param name="scriptName">脚本文件名。</param>
    /// <param name="runtimeIdentifier">目标 runtime identifier。</param>
    [Theory]
    [InlineData("publish-workbench-osx-arm64.ps1", "osx-arm64")]
    [InlineData("publish-workbench-osx-x64.ps1", "osx-x64")]
    public void MacPublishEntriesUseAppBundleAndExtensionlessCli(string scriptName, string runtimeIdentifier)
    {
        var source = ReadScript(scriptName);

        Assert.Contains("publish-workbenchruntime.ps1", source, StringComparison.Ordinal);
        Assert.Contains("-RuntimeIdentifier \"" + runtimeIdentifier + "\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-MacAppBundleName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-GuiEntry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-CliEntry", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Linux 发布入口使用无扩展名 GUI 和 CLI。
    /// </summary>
    [Fact]
    public void LinuxPublishEntryUsesExtensionlessExecutables()
    {
        var source = ReadScript("publish-workbench-linux-x64.ps1");

        Assert.Contains("publish-workbenchruntime.ps1", source, StringComparison.Ordinal);
        Assert.Contains("-RuntimeIdentifier \"linux-x64\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-GuiEntry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-CliEntry", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证三种用户自举入口在工具链脚本目录中都有唯一权威模板，并把输出定向到显式项目缓存。
    /// </summary>
    /// <param name="fileName">自举入口文件名。</param>
    [Theory]
    [InlineData("build-current-platform.cmd")]
    [InlineData("build-current-platform.sh")]
    [InlineData("build-current-platform.command")]
    public void RuntimeBootstrapTemplatesInvokeProjectCacheBootstrapCommand(string fileName)
    {
        var source = ReadBootstrapTemplate(fileName);

        Assert.Contains("runtime bootstrap", source, StringComparison.Ordinal);
        Assert.Contains("--project-root", source, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime publish-current", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Assets/YokiFrame", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证通用 bootstrap 模板允许在缓存完成后直接打开对应 Runtime 中的新 Installer。
    /// </summary>
    /// <param name="fileName">通用 bootstrap 模板文件名。</param>
    [Theory]
    [InlineData("build-current-platform.cmd")]
    [InlineData("build-current-platform.sh")]
    [InlineData("build-current-platform.command")]
    public void RuntimeBootstrapTemplatesSupportOpeningInstaller(string fileName)
    {
        var source = ReadBootstrapTemplate(fileName);

        Assert.Contains("--open-installer", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Godot 用户可从源码包运行专用入口，入口只转发到通用 bootstrap 并请求打开 Installer。
    /// </summary>
    /// <param name="fileName">Godot 安装入口文件名。</param>
    /// <param name="bootstrapFileName">对应平台的通用 bootstrap 文件名。</param>
    [Theory]
    [InlineData("install-godot.cmd", "build-current-platform.cmd")]
    [InlineData("install-godot.sh", "build-current-platform.sh")]
    [InlineData("install-godot.command", "build-current-platform.sh")]
    public void GodotInstallTemplatesBuildRuntimeAndOpenInstaller(
        string fileName,
        string bootstrapFileName)
    {
        var source = ReadBootstrapTemplate(fileName);

        Assert.Contains(bootstrapFileName, source, StringComparison.Ordinal);
        Assert.Contains("--open-installer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime publish-current", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Unix 自举入口只依赖 POSIX shell 与 dotnet，不要求用户额外安装 PowerShell。
    /// </summary>
    /// <param name="fileName">Unix 自举入口文件名。</param>
    [Theory]
    [InlineData("build-current-platform.sh")]
    [InlineData("build-current-platform.command")]
    public void UnixRuntimeBootstrapDoesNotDependOnPowerShell(string fileName)
    {
        var source = ReadBootstrapTemplate(fileName);

        Assert.DoesNotContain("pwsh", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读取发布脚本文本。
    /// </summary>
    /// <param name="scriptName">脚本文件名。</param>
    /// <returns>脚本文本。</returns>
    private static string ReadScript(string scriptName)
    {
        var path = Path.Combine(FindScriptsRoot(), scriptName);
        Assert.True(File.Exists(path), "Missing publish script: " + path);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 读取 Packaging 权威实现源码，用于约束脚本不得重新复制的发布职责。
    /// </summary>
    /// <param name="segments">相对 `src/YokiFrame.Packaging` 的路径片段。</param>
    /// <returns>Packaging 源文件文本。</returns>
    private static string ReadPackagingSource(params string[] segments)
    {
        var pathSegments = new[] { FindPackageRoot(), "YokiFrameWorkbench~", "src", "YokiFrame.Packaging" }
            .Concat(segments)
            .ToArray();
        var path = Path.Combine(pathSegments);
        Assert.True(File.Exists(path), "Missing Packaging source: " + path);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 读取 Workbench src 下指定项目文件。
    /// </summary>
    /// <param name="projectName">目录名及项目名。</param>
    /// <returns>已解析的项目 XML。</returns>
    private static XDocument ReadSourceProject(string projectName)
    {
        var path = Path.Combine(
            FindPackageRoot(),
            "YokiFrameWorkbench~",
            "src",
            projectName,
            projectName + ".csproj");
        Assert.True(File.Exists(path), "Missing Workbench project: " + path);
        return XDocument.Load(path);
    }

    /// <summary>
    /// 读取工具链脚本目录中的 runtime bootstrap 权威模板。
    /// </summary>
    /// <param name="fileName">模板文件名。</param>
    /// <returns>模板文本。</returns>
    private static string ReadBootstrapTemplate(string fileName)
    {
        var path = Path.Combine(FindScriptsRoot(), "runtime-bootstrap", fileName);
        Assert.True(File.Exists(path), "Missing runtime bootstrap template: " + path);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 从当前测试进程目录向上定位 Workbench scripts 目录。
    /// </summary>
    /// <returns>scripts 目录。</returns>
    private static string FindScriptsRoot()
    {
        return Path.Combine(FindPackageRoot(), "YokiFrameWorkbench~", "scripts");
    }

    /// <summary>
    /// 从当前测试进程目录向上定位 YokiFrame 包根。
    /// </summary>
    /// <returns>YokiFrame 包根目录。</returns>
    private static string FindPackageRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var packageRoot = Path.Combine(current.FullName, "Assets", "YokiFrame");
            if (Directory.Exists(Path.Combine(packageRoot, "YokiFrameWorkbench~", "scripts")))
            {
                return packageRoot;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate YokiFrame package root.");
    }
}
