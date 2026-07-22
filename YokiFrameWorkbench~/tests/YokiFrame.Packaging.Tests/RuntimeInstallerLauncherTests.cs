using System.Diagnostics;
using YokiFrame.Packaging.Models;
using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Runtime bootstrap 后打开新 Installer 时的来源、目标和进程参数边界。
/// </summary>
public sealed class RuntimeInstallerLauncherTests
{
    /// <summary>
    /// 验证新 Installer 使用缓存 GUI 入口启动，并显式携带当前源码包和目标项目路径。
    /// </summary>
    [Fact]
    public void LaunchForwardsSourceAndTargetToRuntimeInstaller()
    {
        using RuntimeLauncherFixture fixture = RuntimeLauncherFixture.Create();
        ProcessStartInfo? capturedStartInfo = null;
        RuntimeInstallerLauncher launcher = new(startInfo => capturedStartInfo = startInfo);

        launcher.Launch(fixture.BootstrapResult, fixture.SourcePackageRoot, fixture.TargetProjectRoot);

        var startInfo = Assert.IsType<ProcessStartInfo>(capturedStartInfo);
        Assert.Equal(fixture.GuiPath, startInfo.FileName);
        Assert.Equal(fixture.TargetProjectRoot, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(
            new[]
            {
                "--source",
                fixture.SourcePackageRoot,
                "--target",
                fixture.TargetProjectRoot
            },
            startInfo.ArgumentList.ToArray());
    }

    /// <summary>
    /// 提供已存在 GUI、源码包和项目目录的最小 Runtime bootstrap 结果。
    /// </summary>
    private sealed class RuntimeLauncherFixture : IDisposable
    {
        private readonly string mRoot;

        /// <summary>
        /// 创建包含可启动 GUI 文件的临时 Runtime 目录。
        /// </summary>
        private RuntimeLauncherFixture()
        {
            mRoot = Path.Combine(Path.GetTempPath(), "yokiframe-runtime-launcher-tests", Guid.NewGuid().ToString("N"));
            SourcePackageRoot = Path.Combine(mRoot, "source", "YokiFrame");
            TargetProjectRoot = Path.Combine(mRoot, "project");
            var runtimeRoot = Path.Combine(TargetProjectRoot, ".yokiframe", "runtime");
            GuiPath = Path.Combine(runtimeRoot, "win-x64-aot", "YokiFrame.Workbench.Avalonia.exe");
            Directory.CreateDirectory(SourcePackageRoot);
            Directory.CreateDirectory(TargetProjectRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(GuiPath)!);
            File.WriteAllText(GuiPath, "gui");
            var manifestPath = Path.Combine(runtimeRoot, "tool-manifest.json");
            File.WriteAllText(manifestPath, "{}");
            RuntimePublishResult publishResult = new(
                "win-x64-aot",
                Path.GetDirectoryName(GuiPath)!,
                GuiPath,
                string.Empty,
                manifestPath);
            BootstrapResult = new RuntimeCacheBootstrapResult(
                new string('a', 64),
                runtimeRoot,
                publishResult,
                rebuilt: true);
        }

        /// <summary>
        /// 获取只读源包根。
        /// </summary>
        public string SourcePackageRoot { get; }

        /// <summary>
        /// 获取目标项目根。
        /// </summary>
        public string TargetProjectRoot { get; }

        /// <summary>
        /// 获取缓存 GUI 入口。
        /// </summary>
        public string GuiPath { get; }

        /// <summary>
        /// 获取可传入 Runtime 启动器的 bootstrap 结果。
        /// </summary>
        public RuntimeCacheBootstrapResult BootstrapResult { get; }

        /// <summary>
        /// 创建独立临时 fixture。
        /// </summary>
        /// <returns>可供单个测试使用的 fixture。</returns>
        public static RuntimeLauncherFixture Create()
        {
            return new RuntimeLauncherFixture();
        }

        /// <summary>
        /// 删除测试生成的全部临时文件。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(mRoot))
            {
                Directory.Delete(mRoot, recursive: true);
            }
        }
    }
}
