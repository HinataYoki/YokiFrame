using YokiFrame;
using System.Text.Json;
using System.Xml.Linq;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;
using YokiFrame.Installer.Core.Tests.Transactions;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 锁定 Godot 本地安装的完整 add-on 替换、项目 owner 文件与回滚事务契约。
/// </summary>
public sealed class GodotInstallServiceTests
{
    /// <summary>
    /// 验证成功安装会提交目标 profile、插件入口、唯一主 csproj owner group 和 project.godot owner 项。
    /// </summary>
    [Fact]
    public void ExecuteCommitsPackageAndAllOwnedGodotFiles()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();

        var result = new GodotInstallService().Execute(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            UnmanagedPackagePolicy.Reject);

        Assert.Equal(fixture.AddonRoot, result.PackageResult.TargetPackageRoot);
        Assert.Equal(fixture.ProjectFilePath, result.ProjectFilePath);
        Assert.Equal(fixture.ProjectSettingsPath, result.ProjectSettingsPath);
        Assert.Equal(fixture.PluginConfigPath, result.PluginConfigPath);
        Assert.Equal(fixture.PluginScriptPath, result.PluginScriptPath);
        Assert.Equal(fixture.PluginScriptUidPath, result.PluginScriptUidPath);
        Assert.Equal(fixture.RuntimeBootstrapPath, result.RuntimeBootstrapPath);
        Assert.Equal(fixture.RuntimeBootstrapUidPath, result.RuntimeBootstrapUidPath);
        Assert.False(result.PackageResult.ReplacedExistingPackage);
        Assert.True(File.Exists(result.PackageResult.OwnerManifestPath));
        AssertProjectedPackage(fixture);
        AssertPluginEntryPoints(fixture);
        AssertOwnedProjectFiles(fixture);
    }

    /// <summary>
    /// 验证只读计划完整携带 Application 的 Godot 配置选项和全部稳定目标路径。
    /// </summary>
    [Fact]
    public void CreatePlanCarriesGodotOptionsWithoutWriting()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);
        GodotInstallRequest request = CreateRequest(
            fixture,
            repairProjectSettings: false,
            enablePlugin: false,
            policy: UnmanagedPackagePolicy.Reject);

        var plan = new GodotInstallService().CreatePlan(request);

        Assert.False(plan.RepairProjectSettings);
        Assert.False(plan.EnablePlugin);
        Assert.Equal(fixture.AddonRoot, plan.AddonRoot);
        Assert.Equal(fixture.TargetPackageRoot, plan.TargetPackageRoot);
        Assert.Equal(fixture.ProjectSettingsPath, plan.ProjectSettingsPath);
        Assert.Equal(fixture.PluginScriptUidPath, plan.PluginScriptUidPath);
        Assert.Equal(fixture.RuntimeBootstrapPath, plan.RuntimeBootstrapPath);
        Assert.Equal(fixture.RuntimeBootstrapUidPath, plan.RuntimeBootstrapUidPath);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }

    /// <summary>
    /// 验证缺失项目 Runtime 指针会返回可被 Application 和 Installer 恢复入口识别的前置条件异常。
    /// </summary>
    [Fact]
    public void CreatePlanReportsTypedRuntimeBootstrapRequirement()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        File.Delete(YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(fixture.ProjectRoot));

        var exception = Assert.Throws<InvalidDataException>(
            () => new GodotInstallService().CreatePlan(CreateRequest(fixture)));

        Assert.True(RuntimeCacheBootstrapRequirement.IsRequired(exception));
        Assert.Contains("构建", exception.Message, StringComparison.Ordinal);
        Assert.Contains("install-godot", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Godot 安装计划在任何投影或 owner 检查前拒绝低于下限或非 Godot SDK，并保持项目目录不变。
    /// </summary>
    /// <param name="sdk">待写入顶层项目的 MSBuild SDK。</param>
    /// <param name="targetFramework">待写入顶层项目的 TargetFramework。</param>
    [Theory]
    [InlineData("Godot.NET.Sdk/4.6.0", "net8.0")]
    [InlineData("Godot.NET.Sdk/4.7.0", "net7.0")]
    [InlineData("Microsoft.NET.Sdk", "net8.0")]
    public void CreatePlanRejectsUnsupportedGodotTargetWithoutWriting(string sdk, string targetFramework)
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.WriteTopLevelProjectFile(sdk, targetFramework);
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        var exception = Assert.Throws<InvalidDataException>(() => new GodotInstallService().CreatePlan(CreateRequest(fixture)));

        Assert.Contains("Godot", exception.Message, StringComparison.OrdinalIgnoreCase);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }

    /// <summary>
    /// 验证安装计划接受当前 4.7/net8 基线之后的 Godot .NET SDK 与目标框架。
    /// </summary>
    [Fact]
    public void CreatePlanAcceptsGodotTargetAfterCurrentBaseline()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.WriteTopLevelProjectFile("Godot.NET.Sdk/4.8.0", "net9.0");

        var plan = new GodotInstallService().CreatePlan(CreateRequest(fixture));

        Assert.Equal(fixture.ProjectRoot, plan.ProjectRoot);
    }

    /// <summary>
    /// 验证计划阶段会报告用户脚本真实引用的未迁移 Kit，并在任何安装写入前停止。
    /// </summary>
    [Fact]
    public void CreatePlanRejectsUnsupportedKitReferencesWithoutWriting()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.WriteUserScript(
            "Scripts/LegacyKitSmoke.cs",
            """
            using YokiFrame;
            public sealed class LegacyKitSmoke
            {
                public void Run()
                {
                    InputKit.Reset();
                    InputKit.Update(0.5f);
                    SaveKit.SetMaxSlots(3);
                    var fsm = new FSM<int>("legacy");
                    _ = fsm;
                }
            }
            """);
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        var exception = Assert.Throws<UnsupportedKitReferenceException>(
            () => new GodotInstallService().CreatePlan(CreateRequest(fixture)));

        Assert.Equal(3, exception.Conflicts.Count);
        Assert.Contains("Scripts/LegacyKitSmoke.cs:6", exception.Message, StringComparison.Ordinal);
        Assert.Contains("InputKit", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SaveKit", exception.Message, StringComparison.Ordinal);
        Assert.Contains("FsmKit", exception.Message, StringComparison.Ordinal);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }

    /// <summary>
    /// 验证当前发布包已提供的 Kit 可以继续使用，注释和字符串中的旧 Kit 名称不会制造冲突。
    /// </summary>
    [Fact]
    public void CreatePlanIgnoresAvailableKitsAndTextOnlyMentions()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.WriteUserScript(
            "Scripts/CurrentKitSmoke.cs",
            """
            using YokiFrame;
            public sealed class CurrentKitSmoke
            {
                public void Run()
                {
                    EventKit.String.Send("InputKit is intentionally unavailable");
                    // SaveKit must not be treated as code here.
                    var pool = PoolKit.Create(static () => new object());
                    _ = pool;
                }
            }
            """);

        var plan = new GodotInstallService().CreatePlan(CreateRequest(fixture));

        Assert.Equal(fixture.ProjectRoot, plan.ProjectRoot);
    }

    /// <summary>
    /// 验证关闭 repair 时 project.godot 完全不进入提交事务，但插件入口仍正常生成。
    /// </summary>
    [Fact]
    public void ExecuteLeavesProjectSettingsUntouchedWhenRepairIsDisabled()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        var beforeSettings = File.ReadAllBytes(fixture.ProjectSettingsPath);
        var request = CreateRequest(fixture, repairProjectSettings: false, enablePlugin: true);

        _ = new GodotInstallService().Execute(request);

        Assert.Equal(beforeSettings, File.ReadAllBytes(fixture.ProjectSettingsPath));
        Assert.True(File.Exists(fixture.PluginConfigPath));
        Assert.True(File.Exists(fixture.PluginScriptPath));
        Assert.True(File.Exists(fixture.PluginScriptUidPath));
        Assert.True(File.Exists(fixture.RuntimeBootstrapPath));
        Assert.True(File.Exists(fixture.RuntimeBootstrapUidPath));
    }

    /// <summary>
    /// 验证关闭 enable 时 repair 仍维护 autoload 与 package_root，但不会登记 YokiFrame editor plugin。
    /// </summary>
    [Fact]
    public void ExecuteRepairsOwnedSettingsWithoutRegisteringDisabledPlugin()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        var request = CreateRequest(fixture, repairProjectSettings: true, enablePlugin: false);

        _ = new GodotInstallService().Execute(request);

        var settings = File.ReadAllText(fixture.ProjectSettingsPath);
        Assert.Contains("YokiFrameGodotBootstrap", settings, StringComparison.Ordinal);
        Assert.Contains("[yokiframe]", settings, StringComparison.Ordinal);
        Assert.Contains("res://addons/other/plugin.cfg", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("res://addons/yokiframe/plugin.cfg", settings, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证合法既有 Editor bootstrap UID 会被完整保留。
    /// </summary>
    [Fact]
    public void ExecutePreservesExistingValidPluginScriptUid()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        const string existingUid = "uid://abc123\n";
        fixture.WritePluginScriptUid(existingUid);

        _ = new GodotInstallService().Execute(CreateRequest(fixture));

        Assert.Equal(existingUid, File.ReadAllText(fixture.PluginScriptUidPath));
    }

    /// <summary>
    /// 验证无效 Editor bootstrap UID 会修复为确定性合法值。
    /// </summary>
    [Fact]
    public void ExecuteRepairsInvalidPluginScriptUid()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.WritePluginScriptUid("uid://invalid-z\n");
        GodotUidGenerator generator = new();

        _ = new GodotInstallService().Execute(CreateRequest(fixture));

        var content = File.ReadAllText(fixture.PluginScriptUidPath);
        Assert.Equal(generator.Generate("res://addons/yokiframe/YokiFrameGodotEditorPlugin.cs") + "\n", content);
        Assert.True(generator.IsValid(content));
    }

    /// <summary>
    /// 验证更新会整目录替换旧 add-on，删除不再加载的 plugin.gd 与 UID，只保留正式 C# EditorPlugin。
    /// </summary>
    [Fact]
    public void ExecuteRemovesLegacyGdscriptEntryPointDuringDirectoryReplacement()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.SeedLegacyInstallation();

        _ = new GodotInstallService().Execute(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            UnmanagedPackagePolicy.Reject);

        Assert.False(File.Exists(fixture.LegacyPluginScriptPath));
        Assert.False(File.Exists(fixture.LegacyPluginScriptUidPath));
        Assert.True(File.Exists(fixture.PluginScriptPath));
        Assert.True(File.Exists(fixture.PluginScriptUidPath));
    }

    /// <summary>
    /// 验证无 owner manifest 的旧 add-on 也按完整目录替换，不进行文件级冲突检查或合并。
    /// </summary>
    [Fact]
    public void ExecuteReplacesUnmanagedLegacyWithoutFileLevelMerge()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.SeedLegacyInstallation();
        var result = new GodotInstallService().Execute(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            UnmanagedPackagePolicy.Reject);

        Assert.True(result.PackageResult.ReplacedExistingPackage);
        Assert.False(File.Exists(fixture.GetTargetPackagePath("legacy.marker")));
        Assert.False(File.Exists(fixture.LegacyPluginScriptPath));
        Assert.False(File.Exists(fixture.LegacyPluginScriptUidPath));
        Assert.True(File.Exists(fixture.PluginScriptPath));
    }

    /// <summary>
    /// 验证空 Godot .NET 项目没有顶层 csproj 时，计划阶段只生成内存内容，执行阶段再提交主项目文件。
    /// </summary>
    [Fact]
    public void ExecuteCreatesMissingTopLevelGodotProjectForEmptyDotNetProject()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.RemoveTopLevelProjectFile();
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        var service = new GodotInstallService();
        var plan = service.CreatePlan(CreateRequest(fixture));

        Assert.True(plan.ProjectFileWasGenerated);
        Assert.Equal(fixture.ProjectFilePath, plan.ProjectFilePath);
        Assert.Contains("Godot.NET.Sdk/4.7.0", plan.ProjectFileContent, StringComparison.Ordinal);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));

        service.Execute(CreateRequest(fixture));

        Assert.True(File.Exists(fixture.ProjectFilePath));
        AssertOwnedProjectFiles(fixture);
    }

    /// <summary>
    /// 验证空项目生成的主 csproj 在提交后失败时会随 add-on 一起回滚，不留下半安装状态。
    /// </summary>
    [Fact]
    public void ExecuteRollsBackGeneratedTopLevelProjectWhenCommitFails()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.RemoveTopLevelProjectFile();
        CallbackGodotInstallFaultInjector faultInjector = new(checkpoint =>
        {
            if (checkpoint == GodotInstallCheckpoint.ProjectFileCommitted)
            {
                throw new InvalidOperationException("Injected generated project transaction failure.");
            }
        });

        var exception = Assert.Throws<GodotInstallException>(() => new GodotInstallService(faultInjector).Execute(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            UnmanagedPackagePolicy.Reject));

        Assert.True(exception.RollbackSucceeded);
        Assert.False(File.Exists(fixture.ProjectFilePath));
        Assert.False(Directory.Exists(fixture.AddonRoot));
    }

    /// <summary>
    /// 验证项目根存在多个顶层 csproj 时会零写入拒绝，而不是按文件名猜测主项目。
    /// </summary>
    [Fact]
    public void ExecuteRejectsMultipleTopLevelGodotProjectsWithoutWriting()
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.AddSecondTopLevelProjectFile();
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        var exception = Assert.Throws<InvalidDataException>(() => new GodotInstallService().Execute(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            UnmanagedPackagePolicy.Reject));

        Assert.Contains("csproj", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }

    /// <summary>
    /// 验证任一目录或项目 owner 文件提交后发生故障时，会恢复旧 add-on 与全部项目文件并留下诊断证据。
    /// </summary>
    /// <param name="faultCheckpointName">需要注入故障的事务检查点名称。</param>
    [Theory]
    [InlineData(nameof(GodotInstallCheckpoint.AddonStagingVerified))]
    [InlineData(nameof(GodotInstallCheckpoint.ExistingAddonBackedUp))]
    [InlineData(nameof(GodotInstallCheckpoint.AddonCommitted))]
    [InlineData(nameof(GodotInstallCheckpoint.ProjectFileCommitted))]
    [InlineData(nameof(GodotInstallCheckpoint.ProjectSettingsCommitted))]
    public void ExecuteRollsBackCompleteInstallationWhenTransactionCommitFails(
        string faultCheckpointName)
    {
        using GodotInstallServiceFixture fixture = GodotInstallServiceFixture.Create();
        fixture.SeedLegacyInstallation();
        var faultCheckpoint = Enum.Parse<GodotInstallCheckpoint>(faultCheckpointName);
        CallbackGodotInstallFaultInjector faultInjector = new(checkpoint =>
        {
            if (checkpoint == faultCheckpoint)
            {
                throw new InvalidOperationException("Injected Godot transaction failure.");
            }
        });

        var exception = Assert.Throws<GodotInstallException>(() => new GodotInstallService(faultInjector).Execute(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            GodotInstallServiceFixture.RUNTIME_PROFILE,
            UnmanagedPackagePolicy.Reject));

        Assert.True(exception.RollbackSucceeded);
        fixture.AssertLegacyInstallationRestored();
        AssertFailureEvidence(exception.DiagnosticEvidencePath, faultCheckpoint);
    }

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
        Assert.Equal(builder.BuildPluginConfig(), File.ReadAllText(fixture.PluginConfigPath));
        Assert.Equal(builder.BuildEditorBootstrapScript(), File.ReadAllText(fixture.PluginScriptPath));
        Assert.Equal(builder.BuildRuntimeBootstrapScript(), File.ReadAllText(fixture.RuntimeBootstrapPath));
        var uidContent = File.ReadAllText(fixture.PluginScriptUidPath);
        var runtimeUidContent = File.ReadAllText(fixture.RuntimeBootstrapUidPath);
        Assert.True(new GodotUidGenerator().IsValid(uidContent));
        Assert.True(new GodotUidGenerator().IsValid(runtimeUidContent));
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
        Assert.Equal(
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
                "addons/yokiframe/package/YokiFrame/Tools/SpatialKit/Editor/YokiFrame.SpatialKit.Editor.csproj"
            ],
            projectReferences);
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
        Assert.Equal("<Project Sdk=\"Microsoft.NET.Sdk\" />", File.ReadAllText(fixture.NestedProjectFilePath));

        var settings = File.ReadAllText(fixture.ProjectSettingsPath);
        Assert.Contains("res://addons/yokiframe/plugin.cfg", settings, StringComparison.Ordinal);
        Assert.Contains("YokiFrameGodotBootstrap", settings, StringComparison.Ordinal);
        Assert.Contains("[yokiframe]", settings, StringComparison.Ordinal);
        Assert.Contains("config/name=\"Fixture\"", settings, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证失败异常引用持久化 JSON，并准确记录目录替换检查点与回滚结果。
    /// </summary>
    /// <param name="evidencePath">安装异常公开的诊断证据路径。</param>
    /// <param name="checkpoint">预期故障检查点。</param>
    private static void AssertFailureEvidence(string evidencePath, GodotInstallCheckpoint checkpoint)
    {
        Assert.True(File.Exists(evidencePath));
        using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
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
