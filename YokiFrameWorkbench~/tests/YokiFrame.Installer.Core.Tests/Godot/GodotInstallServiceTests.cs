using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using YokiFrame;
using YokiFrame.Installer.Core.Tests;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;
using YokiFrame.Installer.Core.Tests.Transactions;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 锁定 Godot 本地安装的完整 add-on 替换、项目 owner 文件与回滚事务契约。
/// </summary>
public sealed partial class GodotInstallServiceTests
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
        AssertPluginEntryPoints(fixture);
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

        var settings = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.ProjectSettingsPath));
        Assert.Equal(
            new[] { "enabled=PackedStringArray(\"res://addons/other/plugin.cfg\")" },
            GodotProjectSettingsReader.ReadNonEmptyLines(settings, "editor_plugins"));
        Assert.Equal(
            "\"*res://autoloads/user_service.cs\"",
            GodotProjectSettingsReader.ReadSingleValue(settings, "autoload", "UserService"));
        Assert.Equal(
            "\"*res://addons/yokiframe/YokiFrameGodotBootstrap.cs\"",
            GodotProjectSettingsReader.ReadSingleValue(settings, "autoload", "YokiFrameGodotBootstrap"));
        Assert.Equal(
            "\"res://addons/yokiframe/package/YokiFrame\"",
            GodotProjectSettingsReader.ReadSingleValue(settings, "yokiframe", "package_root"));
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

        InstallerFileAssert.EqualUtf8(fixture.PluginScriptUidPath, existingUid);
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

        var expectedUid = generator.Generate("res://addons/yokiframe/YokiFrameGodotEditorPlugin.cs") + "\n";
        InstallerFileAssert.EqualUtf8(fixture.PluginScriptUidPath, expectedUid);
        Assert.True(generator.IsValid(expectedUid));
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
        fixture.AssertReplacedLegacyEntryPoints();
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
        var generatedProject = XDocument.Parse(plan.ProjectFileContent);
        Assert.Equal("Godot.NET.Sdk/4.7.0", (string?)generatedProject.Root?.Attribute("Sdk"));
        Assert.Equal(
            "net8.0",
            Assert.Single(generatedProject.Descendants(), static element => element.Name.LocalName == "TargetFramework").Value);
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
}
