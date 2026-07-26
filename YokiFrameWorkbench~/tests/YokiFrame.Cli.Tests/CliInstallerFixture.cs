using System.Diagnostics;
using YokiFrame;
using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 为 CLI Installer 进程测试提供统一源包、Unity/Godot 目标和真实子进程执行器。
/// </summary>
internal sealed class CliInstallerFixture : IDisposable
{
    /// <summary>
    /// 创建隔离源包及两个满足最低版本门槛的目标项目。
    /// </summary>
    private CliInstallerFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "yokiframe-cli-installer-tests", Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
        UnityProjectRoot = Path.Combine(Root, "unity-project");
        GodotProjectRoot = Path.Combine(Root, "godot-project");
        WriteSourcePackage();
        WriteUnityProject();
        WriteGodotProject();
        WriteGodotRuntimeCache();
    }

    /// <summary>获取测试临时根。</summary>
    internal string Root { get; }

    /// <summary>获取完整 YokiFrame 源包根。</summary>
    internal string SourcePackageRoot { get; }

    /// <summary>获取 Unity 2022.3 测试项目根。</summary>
    internal string UnityProjectRoot { get; }

    /// <summary>获取 Godot 4.7 .NET 测试项目根。</summary>
    internal string GodotProjectRoot { get; }

    /// <summary>
    /// 创建新的隔离 Installer CLI fixture。
    /// </summary>
    /// <returns>已建立源包和目标项目的 fixture。</returns>
    internal static CliInstallerFixture Create()
    {
        return new CliInstallerFixture();
    }

    /// <summary>
    /// 在 Godot add-on 根写入无 owner manifest 的 legacy 内容。
    /// </summary>
    internal void WriteGodotLegacyPackage()
    {
        WriteText(
            Path.Combine(GodotProjectRoot, "addons", "yokiframe", "legacy.marker"),
            "legacy-package");
    }

    /// <summary>
    /// 获取 Godot 安装后的包内文件路径。
    /// </summary>
    /// <param name="relativePath">包相对路径。</param>
    /// <returns>Godot 正式包内完整路径。</returns>
    internal string GetGodotPackagePath(string relativePath)
    {
        return Path.Combine(
            GodotProjectRoot,
            "addons",
            "yokiframe",
            "package",
            "YokiFrame",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 启动真实 CLI 程序并捕获 stdout、stderr 和退出码。
    /// </summary>
    /// <param name="arguments">传递给 CLI 的参数。</param>
    /// <returns>子进程执行结果。</returns>
    internal static async Task<CliInstallerProcessResult> RunCliAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliInstallerProcessResult(process.ExitCode, await standardOutputTask, await standardErrorTask);
    }

    /// <summary>
    /// 清理测试创建的源包和目标项目。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 写入 Unity 与 Godot 投影均可消费的最小完整发布包。
    /// </summary>
    private void WriteSourcePackage()
    {
        WriteText(Path.Combine(SourcePackageRoot, "package.json"), "{\"name\":\"com.hinatayoki.yokiframe\"}");
        WriteText(Path.Combine(SourcePackageRoot, "Documentation~", "README.md"), "fixture");
        WriteText(Path.Combine(SourcePackageRoot, "Core", "Runtime", "Alpha.cs"), "namespace Fixture; public sealed class Alpha { }");
        WriteText(Path.Combine(SourcePackageRoot, "Core", "Runtime", "Alpha.cs.meta"), "guid: fixture");
        WriteText(
            Path.Combine(SourcePackageRoot, "YokiFrameWorkbench~", "src", "FixtureBuildInput.cs"),
            "namespace Fixture; public sealed class FixtureBuildInput { }");
        WriteText(
            Path.Combine(SourcePackageRoot, "Core", "Adapters", "Godot", "Runtime", "YokiFrame.Godot.Runtime.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        WriteText(
            Path.Combine(SourcePackageRoot, "Core", "Adapters", "Godot", "Runtime", "GodotBootstrap.cs"),
            "namespace Fixture; public sealed class GodotBootstrap { }");
        WriteText(
            Path.Combine(SourcePackageRoot, "Core", "Editor", "YokiFrame.Editor.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteText(
            Path.Combine(SourcePackageRoot, "Core", "Adapters", "Godot", "Editor", "YokiFrame.Godot.Editor.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
        WriteText(
            Path.Combine(SourcePackageRoot, "Tools", "ActionKit", "Runtime", "YokiFrame.ActionKit.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteText(
            Path.Combine(SourcePackageRoot, "Tools", "ActionKit", "Editor", "YokiFrame.ActionKit.Editor.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteText(
            Path.Combine(SourcePackageRoot, "Tools", "AudioKit", "Runtime", "YokiFrame.AudioKit.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteText(
            Path.Combine(SourcePackageRoot, "Tools", "AudioKit", "Adapters", "Godot", "Runtime", "YokiFrame.AudioKit.Godot.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
        WriteText(
            Path.Combine(SourcePackageRoot, "Tools", "AudioKit", "Editor", "YokiFrame.AudioKit.Editor.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    }

    /// <summary>
    /// 写入满足 Unity 2022.3 检测和 manifest 规划的最小项目。
    /// </summary>
    private void WriteUnityProject()
    {
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "Assets"));
        WriteText(
            Path.Combine(UnityProjectRoot, "Packages", "manifest.json"),
            "{\"dependencies\":{\"com.unity.textmeshpro\":\"3.0.6\"}}");
        WriteText(
            Path.Combine(UnityProjectRoot, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 2022.3.62f1\n");
    }

    /// <summary>
    /// 写入满足 Godot 4.7 .NET 与 net8.0 门槛的最小项目。
    /// </summary>
    private void WriteGodotProject()
    {
        WriteText(Path.Combine(GodotProjectRoot, "project.godot"), "config_version=5\n");
        WriteText(
            Path.Combine(GodotProjectRoot, "FirstDemo.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
    }

    /// <summary>
    /// 写入与最小源码包指纹绑定的当前宿主 Runtime 缓存，模拟用户已执行项目 bootstrap。
    /// </summary>
    private void WriteGodotRuntimeCache()
    {
        var runtimeProfile = InstallerInstallOptions.CreateGodotLocal(
            SourcePackageRoot,
            GodotProjectRoot,
            new GodotInstallOptions(repairProjectSettings: false, enablePlugin: false),
            InstallerLegacyPackagePolicy.Reject).RuntimeProfile;
        var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(SourcePackageRoot);
        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(GodotProjectRoot, sourceFingerprint);
        var guiEntry = runtimeProfile + "/YokiFrame.Workbench.Avalonia";
        var cliEntry = runtimeProfile + "/yoki";
        var guiPath = Path.Combine(runtimeRoot, guiEntry.Replace('/', Path.DirectorySeparatorChar));
        var cliPath = Path.Combine(runtimeRoot, cliEntry.Replace('/', Path.DirectorySeparatorChar));
        WriteText(guiPath, "runtime-gui");
        WriteText(cliPath, "runtime-cli");
        WriteText(
            Path.Combine(runtimeRoot, "tool-manifest.json"),
            CreateRuntimeManifestJson(runtimeProfile, guiEntry, guiPath, cliEntry, cliPath));
        WriteText(
            YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(GodotProjectRoot),
            "{\"layoutVersion\":1,\"sourceFingerprint\":\"" + sourceFingerprint + "\"}");
    }

    /// <summary>
    /// 生成包含真实文件摘要的 Runtime manifest，供 CLI 子进程走完整 Installer 门禁。
    /// </summary>
    /// <param name="profile">目标 Runtime profile。</param>
    /// <param name="guiEntry">GUI 相对入口。</param>
    /// <param name="guiPath">GUI 完整路径。</param>
    /// <param name="cliEntry">CLI 相对入口。</param>
    /// <param name="cliPath">CLI 完整路径。</param>
    /// <returns>完整 manifest JSON。</returns>
    private static string CreateRuntimeManifestJson(
        string profile,
        string guiEntry,
        string guiPath,
        string cliEntry,
        string cliPath)
    {
        var files = new[]
        {
            new { relativePath = guiEntry, sizeBytes = new FileInfo(guiPath).Length, sha256 = ComputeSha256(guiPath) },
            new { relativePath = cliEntry, sizeBytes = new FileInfo(cliPath).Length, sha256 = ComputeSha256(cliPath) }
        };
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            manifestVersion = 1,
            layoutVersion = 2,
            product = "YokiFrameTool",
            runtimeRoot = ".",
            platforms = new[]
            {
                new
                {
                    platform = profile,
                    runtimeIdentifier = profile,
                    entrypoint = guiEntry,
                    guiEntry,
                    cliEntry,
                    fileCount = files.Length,
                    totalBytes = files.Sum(static file => file.sizeBytes),
                    files
                }
            }
        });
    }

    /// <summary>
    /// 计算 fixture Runtime 文件的 SHA-256。
    /// </summary>
    /// <param name="path">运行文件完整路径。</param>
    /// <returns>小写十六进制 SHA-256。</returns>
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 写入测试文本并自动建立父目录。
    /// </summary>
    /// <param name="path">目标完整路径。</param>
    /// <param name="content">测试文本。</param>
    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 根据测试输出目录定位同一构建配置下的 CLI 程序。
    /// </summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
        => CliTestHelpers.GetCliAssemblyPath();
}

/// <summary>
/// 表示 Installer CLI 子进程的退出码与两个输出通道。
/// </summary>
/// <param name="ExitCode">进程退出码。</param>
/// <param name="StandardOutput">标准输出文本。</param>
/// <param name="StandardError">标准错误文本。</param>
internal sealed record CliInstallerProcessResult(int ExitCode, string StandardOutput, string StandardError);
