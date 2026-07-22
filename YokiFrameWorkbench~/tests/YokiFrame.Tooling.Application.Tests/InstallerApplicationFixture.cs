using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 提供 Tooling.Application Installer 测试使用的隔离源包、Unity 和 Godot 项目。
/// </summary>
internal sealed class InstallerApplicationFixture : IDisposable
{
    private const string PACKAGE_ID = "com.hinatayoki.yokiframe";

    /// <summary>
    /// 创建完全位于系统临时目录的测试现场。
    /// </summary>
    private InstallerApplicationFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "yokiframe-installer-application-tests", Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
        UnityProjectRoot = Path.Combine(Root, "unity-project");
        GodotProjectRoot = Path.Combine(Root, "godot-project");
        UnknownProjectRoot = Path.Combine(Root, "unknown-project");
        CreateSourcePackage();
        CreateUnityProject();
        CreateGodotProject();
        Directory.CreateDirectory(UnknownProjectRoot);
    }

    /// <summary>
    /// 获取 fixture 总根目录。
    /// </summary>
    internal string Root { get; }

    /// <summary>
    /// 获取最小 YokiFrame 源包根。
    /// </summary>
    internal string SourcePackageRoot { get; }

    /// <summary>
    /// 获取 Unity 2022.3 测试项目根。
    /// </summary>
    internal string UnityProjectRoot { get; }

    /// <summary>
    /// 获取 Godot 4.7 .NET 测试项目根。
    /// </summary>
    internal string GodotProjectRoot { get; }

    /// <summary>
    /// 获取无法识别的测试目录。
    /// </summary>
    internal string UnknownProjectRoot { get; }

    /// <summary>
    /// 获取 Unity manifest 路径。
    /// </summary>
    internal string UnityManifestPath => Path.Combine(UnityProjectRoot, "Packages", "manifest.json");

    /// <summary>
    /// 获取 Unity embedded 包目标根。
    /// </summary>
    internal string UnityPackageRoot => Path.Combine(UnityProjectRoot, "Packages", PACKAGE_ID);

    /// <summary>
    /// 获取 Godot 插件包目标根。
    /// </summary>
    internal string GodotPackageRoot => Path.Combine(GodotProjectRoot, "addons", "yokiframe", "package", "YokiFrame");

    /// <summary>
    /// 获取由 Godot 安装事务完整替换的 add-on 根。
    /// </summary>
    internal string GodotAddonRoot => Path.Combine(GodotProjectRoot, "addons", "yokiframe");

    /// <summary>
    /// 创建新的隔离测试现场。
    /// </summary>
    /// <returns>已完成初始化的 fixture。</returns>
    internal static InstallerApplicationFixture Create()
    {
        return new InstallerApplicationFixture();
    }

    /// <summary>
    /// 在 Unity manifest 中登记现有 YokiFrame Git 来源。
    /// </summary>
    /// <param name="gitUrl">待登记 Git URL。</param>
    internal void SetUnityGitDependency(string gitUrl)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(UnityManifestPath))?.AsObject()
            ?? throw new InvalidDataException("Fixture Unity manifest is empty.");
        var dependencies = manifest["dependencies"]?.AsObject()
            ?? throw new InvalidDataException("Fixture Unity dependencies are missing.");
        dependencies[PACKAGE_ID] = gitUrl;
        File.WriteAllText(UnityManifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// 删除 fixture 创建的全部临时文件。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 创建同时可被 Unity 和 Godot 投影器读取的最小源包。
    /// </summary>
    private void CreateSourcePackage()
    {
        WriteText(Path.Combine(SourcePackageRoot, "package.json"), "{\"name\":\"com.hinatayoki.yokiframe\"}");
        WriteText(Path.Combine(SourcePackageRoot, "Documentation~", "README.md"), "fixture");
        WriteText(Path.Combine(SourcePackageRoot, "Core", "Runtime", "CoreMarker.cs"), "namespace Fixture; public sealed class CoreMarker { }");
        WriteText(
            Path.Combine(SourcePackageRoot, "Core", "Adapters", "Godot", "Runtime", "YokiFrame.Godot.Runtime.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
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
        Directory.CreateDirectory(Path.Combine(SourcePackageRoot, "YokiFrameWorkbench~", "src"));
    }

    /// <summary>
    /// 创建满足最低版本门控的 Unity 项目。
    /// </summary>
    private void CreateUnityProject()
    {
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "Packages"));
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "ProjectSettings"));
        WriteText(
            Path.Combine(UnityProjectRoot, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 2022.3.0f1" + System.Environment.NewLine);
        WriteText(
            UnityManifestPath,
            "{\"dependencies\":{\"com.unity.textmeshpro\":\"3.0.6\"},\"enableLockFile\":true}");
    }

    /// <summary>
    /// 创建满足 Godot 4.7 .NET 门控的项目和基础设置。
    /// </summary>
    private void CreateGodotProject()
    {
        WriteText(
            Path.Combine(GodotProjectRoot, "FirstDemo.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        WriteText(
            Path.Combine(GodotProjectRoot, "project.godot"),
            "config_version=5" + System.Environment.NewLine + System.Environment.NewLine + "[application]" + System.Environment.NewLine + "config/name=\"Fixture\"" + System.Environment.NewLine);
        CreateGodotRuntimeCache();
    }

    /// <summary>
    /// 为当前宿主 Runtime profile 创建与 fixture 源包指纹一致的项目级缓存，模拟用户已运行 bootstrap 的前置条件。
    /// </summary>
    private void CreateGodotRuntimeCache()
    {
        var runtimeProfile = InstallerInstallOptions.CreateGodotLocal(
            SourcePackageRoot,
            GodotProjectRoot,
            new GodotInstallOptions(repairProjectSettings: false, enablePlugin: false),
            InstallerLegacyPackagePolicy.Reject).RuntimeProfile;
        var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(SourcePackageRoot);
        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(GodotProjectRoot, sourceFingerprint);
        var guiEntry = runtimeProfile + "/workbench";
        var cliEntry = runtimeProfile + "/yoki";
        var guiPath = Path.Combine(runtimeRoot, guiEntry.Replace('/', Path.DirectorySeparatorChar));
        var cliPath = Path.Combine(runtimeRoot, cliEntry.Replace('/', Path.DirectorySeparatorChar));
        WriteText(
            YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(GodotProjectRoot),
            JsonSerializer.Serialize(new { layoutVersion = 1, sourceFingerprint }));
        WriteText(guiPath, "runtime-gui");
        WriteText(cliPath, "runtime-cli");
        WriteText(
            Path.Combine(runtimeRoot, "tool-manifest.json"),
            CreateRuntimeManifestJson(runtimeProfile, guiEntry, guiPath, cliEntry, cliPath));
    }

    /// <summary>
    /// 生成与两个 fixture 入口完全一致的 Runtime manifest。
    /// </summary>
    /// <param name="profile">目标 Runtime profile。</param>
    /// <param name="guiEntry">GUI 相对入口。</param>
    /// <param name="guiPath">GUI 完整路径。</param>
    /// <param name="cliEntry">CLI 相对入口。</param>
    /// <param name="cliPath">CLI 完整路径。</param>
    /// <returns>带文件长度和 SHA-256 的 manifest JSON。</returns>
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
        return JsonSerializer.Serialize(new
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
    /// 写入文本并自动创建父目录。
    /// </summary>
    /// <param name="path">目标路径。</param>
    /// <param name="content">文本内容。</param>
    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
