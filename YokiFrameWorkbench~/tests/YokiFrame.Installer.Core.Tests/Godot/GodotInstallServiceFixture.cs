using YokiFrame;
using YokiFrame.Installer.Core.Services;
using System.Text.Json;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 为 Godot Installer 端到端测试提供隔离源包、目标项目和旧安装状态。
/// </summary>
internal sealed class GodotInstallServiceFixture : IDisposable
{
    internal const string RUNTIME_PROFILE = "win-x64";

    private const string ORIGINAL_PROJECT_FILE = """
        <Project Sdk="Godot.NET.Sdk/4.7.0">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup Label="UserOwned">
            <None Include="user.asset" />
          </ItemGroup>
        </Project>
        """;

    private const string ORIGINAL_PROJECT_SETTINGS = """
        ; fixture-owned header
        config_version=5

        [application]
        config/name="Fixture"

        [dotnet]
        project/assembly_name="FirstDemo"

        [editor_plugins]
        enabled=PackedStringArray("res://addons/other/plugin.cfg")

        [autoload]
        UserService="*res://autoloads/user_service.cs"
        """;

    private const string ORIGINAL_PLUGIN_CONFIG = "legacy-plugin-config";
    private const string ORIGINAL_PLUGIN_SCRIPT = "legacy-plugin-script";
    private const string ORIGINAL_PLUGIN_SCRIPT_UID = "uid://abc123\n";
    private const string ORIGINAL_LEGACY_PLUGIN_SCRIPT = "legacy-gdscript-entry";
    private const string ORIGINAL_LEGACY_PLUGIN_SCRIPT_UID = "uid://legacy123\n";
    private const string ORIGINAL_PACKAGE_MARKER = "legacy-package";
    private const string NESTED_PROJECT_FILE = "<Project Sdk=\"Microsoft.NET.Sdk\" />";

    /// <summary>
    /// 创建完全位于系统临时目录的 fixture，并写入可被真实投影器消费的最小源包。
    /// </summary>
    private GodotInstallServiceFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "yokiframe-godot-install-tests", Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
        ProjectRoot = Path.Combine(Root, "project");
        AddonRoot = Path.Combine(ProjectRoot, "addons", "yokiframe");
        TargetPackageRoot = Path.Combine(ProjectRoot, "addons", "yokiframe", "package", "YokiFrame");
        ProjectFilePath = Path.Combine(ProjectRoot, "FirstDemo.csproj");
        ProjectSettingsPath = Path.Combine(ProjectRoot, "project.godot");
        PluginConfigPath = Path.Combine(ProjectRoot, "addons", "yokiframe", "plugin.cfg");
        PluginScriptPath = Path.Combine(ProjectRoot, "addons", "yokiframe", "YokiFrameGodotEditorPlugin.cs");
        PluginScriptUidPath = PluginScriptPath + ".uid";
        LegacyPluginScriptPath = Path.Combine(ProjectRoot, "addons", "yokiframe", "plugin.gd");
        LegacyPluginScriptUidPath = LegacyPluginScriptPath + ".uid";
        RuntimeBootstrapPath = Path.Combine(ProjectRoot, "addons", "yokiframe", "YokiFrameGodotBootstrap.cs");
        RuntimeBootstrapUidPath = RuntimeBootstrapPath + ".uid";
        NestedProjectFilePath = Path.Combine(ProjectRoot, "nested", "Nested.csproj");

        Directory.CreateDirectory(SourcePackageRoot);
        Directory.CreateDirectory(ProjectRoot);
        WriteSourcePackage();
        WriteRuntimeCache();
        WriteText(ProjectFilePath, ORIGINAL_PROJECT_FILE);
        WriteText(ProjectSettingsPath, ORIGINAL_PROJECT_SETTINGS);
        WriteText(NestedProjectFilePath, NESTED_PROJECT_FILE);
    }

    /// <summary>
    /// 获取 fixture 的完整临时根目录。
    /// </summary>
    internal string Root { get; }

    /// <summary>
    /// 获取待投影的 YokiFrame 源包根。
    /// </summary>
    internal string SourcePackageRoot { get; }

    /// <summary>
    /// 获取模拟 Godot 4.7 .NET 项目根。
    /// </summary>
    internal string ProjectRoot { get; }

    /// <summary>
    /// 获取由 Installer 整目录替换的 Godot add-on 根。
    /// </summary>
    internal string AddonRoot { get; }

    /// <summary>
    /// 获取受管 Godot 包的正式目标目录。
    /// </summary>
    internal string TargetPackageRoot { get; }

    /// <summary>
    /// 获取唯一顶层 Godot C# 项目文件路径。
    /// </summary>
    internal string ProjectFilePath { get; }

    /// <summary>
    /// 获取 project.godot 路径。
    /// </summary>
    internal string ProjectSettingsPath { get; }

    /// <summary>
    /// 获取外层 plugin.cfg 路径。
    /// </summary>
    internal string PluginConfigPath { get; }

    /// <summary>
    /// 获取外层薄 C# EditorPlugin bootstrap 路径。
    /// </summary>
    internal string PluginScriptPath { get; }

    /// <summary>
    /// 获取外层 EditorPlugin bootstrap UID 路径。
    /// </summary>
    internal string PluginScriptUidPath { get; }

    /// <summary>获取升级时应由 Installer 删除的旧 plugin.gd 路径。</summary>
    internal string LegacyPluginScriptPath { get; }

    /// <summary>获取升级时应由 Installer 删除的旧 plugin.gd.uid 路径。</summary>
    internal string LegacyPluginScriptUidPath { get; }

    /// <summary>
    /// 获取编入宿主主程序集的薄 Runtime bootstrap 脚本路径。
    /// </summary>
    internal string RuntimeBootstrapPath { get; }

    /// <summary>
    /// 获取薄 Runtime bootstrap 的稳定 UID sidecar 路径。
    /// </summary>
    internal string RuntimeBootstrapUidPath { get; }

    /// <summary>
    /// 获取用于证明只扫描顶层项目文件的嵌套 csproj 路径。
    /// </summary>
    internal string NestedProjectFilePath { get; }

    /// <summary>
    /// 创建新的隔离 Godot 安装 fixture。
    /// </summary>
    /// <returns>已创建源包和目标项目的 fixture。</returns>
    internal static GodotInstallServiceFixture Create()
    {
        return new GodotInstallServiceFixture();
    }

    /// <summary>
    /// 写入没有 owner manifest 的旧包及旧插件文件，用于接管和回滚场景。
    /// </summary>
    internal void SeedLegacyInstallation()
    {
        WriteText(GetTargetPackagePath("legacy.marker"), ORIGINAL_PACKAGE_MARKER);
        WriteText(PluginConfigPath, ORIGINAL_PLUGIN_CONFIG);
        WriteText(PluginScriptPath, ORIGINAL_PLUGIN_SCRIPT);
        WriteText(PluginScriptUidPath, ORIGINAL_PLUGIN_SCRIPT_UID);
        WriteText(LegacyPluginScriptPath, ORIGINAL_LEGACY_PLUGIN_SCRIPT);
        WriteText(LegacyPluginScriptUidPath, ORIGINAL_LEGACY_PLUGIN_SCRIPT_UID);
    }

    /// <summary>
    /// 写入指定 EditorPlugin bootstrap UID 内容，用于合法保留和无效修复测试。
    /// </summary>
    /// <param name="content">完整 UID sidecar 文本。</param>
    internal void WritePluginScriptUid(string content)
    {
        WriteText(PluginScriptUidPath, content);
    }

    /// <summary>
    /// 在项目根添加另一个顶层 csproj，用于构造主项目选择歧义。
    /// </summary>
    internal void AddSecondTopLevelProjectFile()
    {
        WriteText(Path.Combine(ProjectRoot, "SecondDemo.csproj"), NESTED_PROJECT_FILE);
    }

    /// <summary>
    /// 删除默认顶层 csproj，用于验证缺少主项目时零写入拒绝。
    /// </summary>
    internal void RemoveTopLevelProjectFile()
    {
        File.Delete(ProjectFilePath);
    }

    /// <summary>
    /// 重写唯一顶层项目文件的 SDK 与目标框架，用于验证安装服务自身拒绝不受支持的 Godot 项目。
    /// </summary>
    /// <param name="sdk">MSBuild Project 的 SDK 属性。</param>
    /// <param name="targetFramework">项目的 TargetFramework。</param>
    internal void WriteTopLevelProjectFile(string sdk, string targetFramework)
    {
        WriteText(
            ProjectFilePath,
            "<Project Sdk=\"" + sdk + "\">\n"
            + "  <PropertyGroup>\n"
            + "    <TargetFramework>" + targetFramework + "</TargetFramework>\n"
            + "  </PropertyGroup>\n"
            + "</Project>\n");
    }

    /// <summary>
    /// 在目标项目写入用户 C# 脚本，用于验证安装前的旧 Kit 引用扫描。
    /// </summary>
    /// <param name="relativePath">项目根下的脚本相对路径。</param>
    /// <param name="content">完整 C# 源码。</param>
    internal void WriteUserScript(string relativePath, string content)
    {
        WriteText(
            Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            content);
    }

    /// <summary>
    /// 获取受管包内指定相对路径的完整目标路径。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <returns>目标包内的完整路径。</returns>
    internal string GetTargetPackagePath(string relativePath)
    {
        return Path.Combine(TargetPackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 断言旧包、插件入口和项目文件均已恢复，且新投影未残留在正式目录。
    /// </summary>
    internal void AssertLegacyInstallationRestored()
    {
        Assert.Equal(ORIGINAL_PACKAGE_MARKER, File.ReadAllText(GetTargetPackagePath("legacy.marker")));
        Assert.False(File.Exists(GetTargetPackagePath("Core/Runtime/CoreMarker.cs")));
        Assert.False(File.Exists(new PackageOwnerManifestStore().GetManifestPath(AddonRoot)));
        Assert.Equal(ORIGINAL_PLUGIN_CONFIG, File.ReadAllText(PluginConfigPath));
        Assert.Equal(ORIGINAL_PLUGIN_SCRIPT, File.ReadAllText(PluginScriptPath));
        Assert.Equal(ORIGINAL_PLUGIN_SCRIPT_UID, File.ReadAllText(PluginScriptUidPath));
        Assert.Equal(ORIGINAL_LEGACY_PLUGIN_SCRIPT, File.ReadAllText(LegacyPluginScriptPath));
        Assert.Equal(ORIGINAL_LEGACY_PLUGIN_SCRIPT_UID, File.ReadAllText(LegacyPluginScriptUidPath));
        Assert.False(File.Exists(RuntimeBootstrapPath));
        Assert.False(File.Exists(RuntimeBootstrapUidPath));
        Assert.Equal(ORIGINAL_PROJECT_FILE, File.ReadAllText(ProjectFilePath));
        Assert.Equal(ORIGINAL_PROJECT_SETTINGS, File.ReadAllText(ProjectSettingsPath));
    }

    /// <summary>
    /// 删除 fixture 创建的临时目录及可能保留的诊断证据。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 写入包含目标 profile、排除项和 Godot Adapter 项目的代表性源包。
    /// </summary>
    private void WriteSourcePackage()
    {
        WriteSourceFile("Core/Runtime/CoreMarker.cs", "namespace Fixture; public sealed class CoreMarker { }");
        WriteSourceFile("Core/Runtime/YokiFrame.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile("Core/Runtime/ScriptTool.gd", "extends Node");
        WriteSourceFile("Core/Runtime/ProjectConfig.cfg", "fixture=true");
        WriteSourceFile("Core/Runtime/CoreMarker.cs.uid", "uid://invalid-z");
        WriteSourceFile(
            "Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj",
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
        WriteSourceFile(
            "Core/Editor/YokiFrame.Editor.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile(
            "Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj",
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
        WriteSourceFile(
            "Tools/ActionKit/Runtime/YokiFrame.ActionKit.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile(
            "Tools/ActionKit/Editor/YokiFrame.ActionKit.Editor.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile(
            "Tools/AudioKit/Runtime/YokiFrame.AudioKit.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile(
            "Tools/AudioKit/Adapters/Godot/Runtime/YokiFrame.AudioKit.Godot.csproj",
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
        WriteSourceFile(
            "Tools/AudioKit/Editor/YokiFrame.AudioKit.Editor.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile(
            "Tools/SaveKit/Runtime/YokiFrame.SaveKit.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile(
            "Tools/SaveKit/Adapters/Godot/Runtime/YokiFrame.SaveKit.Godot.csproj",
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\" />");
        WriteSourceFile(
            "Tools/SaveKit/Editor/YokiFrame.SaveKit.Editor.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteSourceFile("YokiFrameWorkbench~/src/FixtureBuildInput.cs", "namespace Fixture; public sealed class FixtureBuildInput { }");
        WriteSourceFile("WorkbenchRuntime~/win-x64/yoki.dll", "selected-runtime");
        WriteSourceFile("WorkbenchRuntime~/linux-x64/yoki.dll", "other-runtime");
        WriteSourceFile("Tests/Ignored.cs", "ignored-test");
        WriteSourceFile("YokiFrameWorkbench~/ignored.txt", "ignored-tool-source");
        WriteSourceFile("Core/Runtime/CoreMarker.cs.meta", "ignored-unity-meta");
    }

    /// <summary>
    /// 为目标项目写入与最小 Workbench 输入匹配的 Runtime 缓存，模拟用户已手动执行 bootstrap。
    /// </summary>
    private void WriteRuntimeCache()
    {
        var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(SourcePackageRoot);
        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(ProjectRoot, sourceFingerprint);
        const string guiEntry = "win-x64/YokiFrame.Workbench.Avalonia.exe";
        const string cliEntry = "win-x64/yoki.exe";
        var guiPath = Path.Combine(runtimeRoot, guiEntry.Replace('/', Path.DirectorySeparatorChar));
        var cliPath = Path.Combine(runtimeRoot, cliEntry.Replace('/', Path.DirectorySeparatorChar));
        WriteText(guiPath, "runtime-gui");
        WriteText(cliPath, "runtime-cli");
        WriteText(
            Path.Combine(runtimeRoot, "tool-manifest.json"),
            CreateRuntimeManifestJson(RUNTIME_PROFILE, guiEntry, guiPath, cliEntry, cliPath));
        WriteText(
            YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(ProjectRoot),
            "{\"layoutVersion\":1,\"sourceFingerprint\":\"" + sourceFingerprint + "\"}");
    }

    /// <summary>
    /// 生成包含文件长度与哈希的真实 Runtime manifest，避免 fixture 绕过生产完整性契约。
    /// </summary>
    /// <param name="profile">目标 Runtime profile。</param>
    /// <param name="guiEntry">GUI 相对入口。</param>
    /// <param name="guiPath">GUI 完整路径。</param>
    /// <param name="cliEntry">CLI 相对入口。</param>
    /// <param name="cliPath">CLI 完整路径。</param>
    /// <returns>可通过共享完整性校验的 JSON。</returns>
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
    /// 计算 fixture 运行文件的 SHA-256，确保测试 manifest 与生产格式一致。
    /// </summary>
    /// <param name="path">运行文件完整路径。</param>
    /// <returns>小写十六进制 SHA-256。</returns>
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 在源包内写入一个相对文件，并自动建立父目录。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的源包相对路径。</param>
    /// <param name="content">测试文件内容。</param>
    private void WriteSourceFile(string relativePath, string content)
    {
        WriteText(
            Path.Combine(SourcePackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            content);
    }

    /// <summary>
    /// 写入测试文本并确保父目录存在。
    /// </summary>
    /// <param name="path">目标完整路径。</param>
    /// <param name="content">待写入内容。</param>
    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}

/// <summary>
/// 将 Godot 安装检查点转交给测试回调，以便在真实外层文件提交后注入故障。
/// </summary>
internal sealed class CallbackGodotInstallFaultInjector : IGodotInstallFaultInjector
{
    private readonly Action<GodotInstallCheckpoint> mCallback;

    /// <summary>
    /// 创建回调式 Godot 安装故障注入器。
    /// </summary>
    /// <param name="callback">每次越过稳定检查点时执行的测试回调。</param>
    internal CallbackGodotInstallFaultInjector(Action<GodotInstallCheckpoint> callback)
    {
        mCallback = callback;
    }

    /// <summary>
    /// 把当前检查点交给测试，由测试决定是否抛出预期故障。
    /// </summary>
    /// <param name="checkpoint">刚完成提交的外层文件检查点。</param>
    public void OnCheckpoint(GodotInstallCheckpoint checkpoint)
    {
        mCallback(checkpoint);
    }
}
