using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;
using YokiFrame.Installer.Core.Tests;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// Godot 安装服务测试的可观察断言。与用例拆开，避免单文件超过行数上限。
/// </summary>
public sealed partial class GodotInstallServiceTests
{
    private static readonly string[] sExpectedGodotProjectReferences =
    [
        "addons/yokiframe/package/YokiFrame/Core/Runtime/YokiFrame.csproj",
        "addons/yokiframe/package/YokiFrame/Core/Editor/YokiFrame.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/ActionKit/Runtime/YokiFrame.ActionKit.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/AudioKit/Runtime/YokiFrame.AudioKit.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/AudioKit/Adapters/Godot/Runtime/YokiFrame.AudioKit.Godot.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/SaveKit/Runtime/YokiFrame.SaveKit.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/SaveKit/Adapters/Godot/Runtime/YokiFrame.SaveKit.Godot.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/SpatialKit/Runtime/YokiFrame.SpatialKit.csproj",
        "addons/yokiframe/package/YokiFrame/Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/ActionKit/Editor/YokiFrame.ActionKit.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/AudioKit/Editor/YokiFrame.AudioKit.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/SaveKit/Editor/YokiFrame.SaveKit.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/SpatialKit/Editor/YokiFrame.SpatialKit.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/RoslynKit/Editor/YokiFrame.RoslynKit.Editor.csproj",
        "addons/yokiframe/package/YokiFrame/Tools/RoslynKit/Adapters/Godot/Runtime/YokiFrame.RoslynKit.Godot.csproj"
    ];

    /// <summary>
    /// 验证受控投影不携带任何 Runtime 产物，并排除测试、工具源码和 Unity meta。
    /// </summary>
    /// <param name="fixture">已完成安装的测试项目。</param>
    private static void AssertProjectedPackage(GodotInstallServiceFixture fixture)
    {
        Assert.True(File.Exists(fixture.GetTargetPackagePath("Core/Runtime/CoreMarker.cs")));
        Assert.True(File.Exists(fixture.GetTargetPackagePath("Core/Runtime/CoreMarker.cs.uid")));
        Assert.True(File.Exists(fixture.GetTargetPackagePath("Core/Runtime/ScriptTool.gd.uid")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath("Core/Runtime/ProjectConfig.cfg.uid")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath("WorkbenchRuntime~/win-x64/yoki.dll")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath("WorkbenchRuntime~/linux-x64/yoki.dll")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath("Tests/Ignored.cs")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath("YokiFrameWorkbench~/ignored.txt")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath("Core/Runtime/CoreMarker.cs.meta")));
        Assert.True(File.Exists(fixture.GetTargetPackagePath(
            "Tools/RoslynKit/Editor/Dependencies~/roslyn-4.8.0/YokiFrame.RoslynKit.Compiler.dll")));
        Assert.False(File.Exists(fixture.GetTargetPackagePath(
            "Tools/RoslynKit/Adapters/Unity/Editor/Dependencies~/roslyn-4.8.0/YokiFrame.RoslynKit.Compiler.dll")));
        var ownerManifest = new PackageOwnerManifestStore().Read(fixture.AddonRoot);
        Assert.Contains(ownerManifest.Files, static file => file.RelativePath == "package/YokiFrame/Core/Runtime/CoreMarker.cs.uid");
        Assert.Contains(ownerManifest.Files, static file => file.RelativePath == "package/YokiFrame/Core/Runtime/ScriptTool.gd.uid");
        Assert.DoesNotContain(ownerManifest.Files, static file => file.RelativePath.EndsWith(".cfg.uid", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证外层插件入口来自统一 builder，且不保留旧工具启动逻辑。
    /// </summary>
    /// <param name="fixture">已完成安装的测试项目。</param>
    private static void AssertPluginEntryPoints(GodotInstallServiceFixture fixture)
    {
        GodotPluginEntryPointBuilder builder = new();
        GodotUidGenerator generator = new();
        InstallerFileAssert.EqualUtf8(fixture.PluginConfigPath, builder.BuildPluginConfig(fixture.SourcePackageRoot));
        InstallerFileAssert.EqualUtf8(fixture.PluginScriptPath, builder.BuildEditorBootstrapScript());
        InstallerFileAssert.EqualUtf8(fixture.RuntimeBootstrapPath, builder.BuildRuntimeBootstrapScript());
        var editorUid = generator.Generate("res://addons/yokiframe/YokiFrameGodotEditorPlugin.cs") + "\n";
        var runtimeUid = generator.Generate("res://addons/yokiframe/YokiFrameGodotBootstrap.cs") + "\n";
        InstallerFileAssert.EqualUtf8(fixture.PluginScriptUidPath, editorUid);
        InstallerFileAssert.EqualUtf8(fixture.RuntimeBootstrapUidPath, runtimeUid);
        Assert.True(generator.IsValid(editorUid));
        Assert.True(generator.IsValid(runtimeUid));
        Assert.False(File.Exists(fixture.PluginConfigPath + ".uid"));
    }

    /// <summary>
    /// 验证唯一顶层项目被 patch，嵌套 csproj 保持原样，project.godot 拥有约定的三类 owner 项。
    /// </summary>
    /// <param name="fixture">已完成安装的测试项目。</param>
    private static void AssertOwnedProjectFiles(GodotInstallServiceFixture fixture)
    {
        var project = XDocument.Load(fixture.ProjectFilePath);
        var ownerGroup = Assert.Single(project.Root!.Elements(), static element =>
            element.Name.LocalName == "ItemGroup"
            && string.Equals((string?)element.Attribute("Label"), "YokiFrame", StringComparison.Ordinal));
        var projectReferences = ownerGroup.Elements()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();
        Assert.Equal(sExpectedGodotProjectReferences, projectReferences);
        var editorReference = Assert.Single(ownerGroup.Elements(), static element =>
            element.Name.LocalName == "ProjectReference"
            && ((string?)element.Attribute("Include"))?.EndsWith(
                "/YokiFrame.Godot.Editor.csproj",
                StringComparison.Ordinal) == true);
        Assert.Equal(
            "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
            (string?)editorReference.Attribute("Condition"));
        Assert.Equal(
            "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=True",
            Assert.Single(editorReference.Elements()).Value);
        InstallerFileAssert.EqualUtf8(fixture.NestedProjectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var settings = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.ProjectSettingsPath));
        Assert.Equal(
            "PackedStringArray(\"res://addons/other/plugin.cfg\", \"res://addons/yokiframe/plugin.cfg\")",
            GodotProjectSettingsReader.ReadSingleValue(settings, "editor_plugins", "enabled"));
        Assert.Equal(
            "\"*res://addons/yokiframe/YokiFrameGodotBootstrap.cs\"",
            GodotProjectSettingsReader.ReadSingleValue(settings, "autoload", "YokiFrameGodotBootstrap"));
        Assert.Equal(
            "\"res://addons/yokiframe/package/YokiFrame\"",
            GodotProjectSettingsReader.ReadSingleValue(settings, "yokiframe", "package_root"));
        Assert.Equal("\"Fixture\"", GodotProjectSettingsReader.ReadSingleValue(settings, "application", "config/name"));
    }

    /// <summary>
    /// 验证失败异常引用持久化 JSON，并准确记录目录替换检查点与回滚结果。
    /// </summary>
    /// <param name="evidencePath">安装异常公开的诊断证据路径。</param>
    /// <param name="checkpoint">预期故障检查点。</param>
    private static void AssertFailureEvidence(string evidencePath, GodotInstallCheckpoint checkpoint)
    {
        Assert.True(File.Exists(evidencePath));
        using var document = JsonDocument.Parse(File.ReadAllBytes(evidencePath));
        Assert.Equal(checkpoint.ToString(), document.RootElement.GetProperty("checkpoint").GetString());
        Assert.True(document.RootElement.GetProperty("rollbackSucceeded").GetBoolean());
        Assert.Contains(
            "Injected Godot transaction failure.",
            document.RootElement.GetProperty("error").GetString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建 typed Godot 安装请求，统一测试默认的 repair、enable 和接管策略。
    /// </summary>
    /// <param name="fixture">Godot 安装 fixture。</param>
    /// <param name="repairProjectSettings">是否提交 project.godot owner 修复。</param>
    /// <param name="enablePlugin">repair 开启时是否登记 editor plugin。</param>
    /// <param name="policy">legacy 包接管策略。</param>
    /// <returns>可直接用于计划或执行的 typed 请求。</returns>
    private static GodotInstallRequest CreateRequest(
        GodotInstallServiceFixture fixture,
        bool repairProjectSettings = true,
        bool enablePlugin = true,
        UnmanagedPackagePolicy policy = UnmanagedPackagePolicy.Reject)
    {
        return new GodotInstallRequest(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            repairProjectSettings,
            enablePlugin,
            policy);
    }
}
