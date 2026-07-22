using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖真实 Installer.Core typed plan/result 到 Application DTO 的映射。
/// </summary>
public sealed class InstallerCoreWorkflowGatewayTests
{
    private const string GIT_URL = "https://github.com/HinataYoki/YokiFrame.git?path=Assets/YokiFrame";

    /// <summary>
    /// 验证 Unity 本地预览同时表达 embedded 安装和本地 file 依赖登记。
    /// </summary>
    [Fact]
    public async Task CreatePlanAsyncMapsUnityLocalSourceAndMutualExclusionActions()
    {
        using var fixture = InstallerApplicationFixture.Create();
        fixture.SetUnityGitDependency(GIT_URL);
        var options = InstallerInstallOptions.CreateUnityLocal(
            fixture.SourcePackageRoot,
            fixture.UnityProjectRoot,
            InstallerLegacyPackagePolicy.Reject);

        var plan = await new InstallerCoreWorkflowGateway().CreatePlanAsync(options, CancellationToken.None);

        Assert.Equal(InstallerTargetKind.Unity, plan.Engine);
        Assert.Equal(InstallerInstallMode.UnityLocal, plan.Mode);
        Assert.Equal(fixture.SourcePackageRoot, plan.Source);
        Assert.Equal(fixture.UnityPackageRoot, plan.PackageTarget);
        Assert.Contains(plan.Actions, action => action.Kind == InstallerPlanActionKind.InstallPackage);
        Assert.Contains(plan.Actions, action => action.Kind == InstallerPlanActionKind.SetEmbeddedDependency);
    }

    /// <summary>
    /// 验证 Unity 受管包被本地修改后仍生成可执行计划，并把覆盖影响投影为非阻断警告。
    /// </summary>
    [Fact]
    public async Task CreatePlanAsyncProjectsManagedModificationsAsReplacementWarning()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var options = InstallerInstallOptions.CreateUnityLocal(
            fixture.SourcePackageRoot,
            fixture.UnityProjectRoot,
            InstallerLegacyPackagePolicy.Reject);
        var gateway = new InstallerCoreWorkflowGateway();
        var initialPlan = await gateway.CreatePlanAsync(options, CancellationToken.None);
        _ = await gateway.ExecuteAsync(
            options,
            initialPlan,
            new ProgressRecorder(new List<InstallerProgressStage>()),
            CancellationToken.None);
        var modifiedPath = Path.Combine(fixture.UnityPackageRoot, "Core", "Runtime", "CoreMarker.cs");
        File.WriteAllText(modifiedPath, "manual-change");

        var replacementPlan = await gateway.CreatePlanAsync(options, CancellationToken.None);

        Assert.Contains(replacementPlan.Actions, action => action.Kind == InstallerPlanActionKind.InstallPackage);
        Assert.Contains(replacementPlan.Warnings, warning => warning.Contains("本地修改", StringComparison.Ordinal));
        Assert.Contains(replacementPlan.Warnings, warning => warning.Contains("完整替换", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 Unity Git 预览把 Git URL 作为唯一来源且只计划 manifest 动作。
    /// </summary>
    [Fact]
    public async Task CreatePlanAsyncMapsUnityGitWithoutFakePackageTransaction()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var options = InstallerInstallOptions.CreateUnityGit(fixture.UnityProjectRoot, GIT_URL);

        var plan = await new InstallerCoreWorkflowGateway().CreatePlanAsync(options, CancellationToken.None);

        Assert.Equal(GIT_URL, plan.Source);
        Assert.Equal(fixture.UnityPackageRoot, plan.PackageTarget);
        Assert.Contains(plan.Actions, action => action.Kind == InstallerPlanActionKind.SetGitDependency);
        Assert.DoesNotContain(plan.Actions, action => action.Kind == InstallerPlanActionKind.InstallPackage);
    }

    /// <summary>
    /// 验证 Unity Git 执行结果指向 manifest，并准确表达内容变化而不伪造包事务。
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncMapsUnityGitResultAndProgress()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var options = InstallerInstallOptions.CreateUnityGit(fixture.UnityProjectRoot, GIT_URL);
        var gateway = new InstallerCoreWorkflowGateway();
        var plan = await gateway.CreatePlanAsync(options, CancellationToken.None);
        List<InstallerProgressStage> stages = new();

        var result = await gateway.ExecuteAsync(options, plan, new ProgressRecorder(stages), CancellationToken.None);

        Assert.Equal(fixture.UnityManifestPath, result.TargetPath);
        Assert.True(result.Changed);
        Assert.False(result.ReplacedExistingPackage);
        Assert.Contains(fixture.UnityManifestPath, result.EvidencePaths);
        Assert.Equal(new[] { InstallerProgressStage.Applying, InstallerProgressStage.Verifying }, stages);
    }

    /// <summary>
    /// 验证预览生成后输入变化会拒绝旧 token，避免执行已过期 Git URL。
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncRejectsPlanWhenGitUrlChangedAfterPreview()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var gateway = new InstallerCoreWorkflowGateway();
        var plannedOptions = InstallerInstallOptions.CreateUnityGit(fixture.UnityProjectRoot, GIT_URL);
        var plan = await gateway.CreatePlanAsync(plannedOptions, CancellationToken.None);
        var changedOptions = InstallerInstallOptions.CreateUnityGit(
            fixture.UnityProjectRoot,
            "https://github.com/HinataYoki/YokiFrame.git?path=Other");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ExecuteAsync(
            changedOptions,
            plan,
            new ProgressRecorder(new List<InstallerProgressStage>()),
            CancellationToken.None));

        Assert.DoesNotContain("com.hinatayoki.yokiframe", File.ReadAllText(fixture.UnityManifestPath), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Godot 预览以完整 add-on 替换表达包、插件和 bootstrap，并保留外部 owner 动作。
    /// </summary>
    [Fact]
    public async Task CreatePlanAsyncMapsGodotPackageAndOuterOwnerActions()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var addonRoot = Path.Combine(fixture.GodotProjectRoot, "addons", "yokiframe");
        Directory.CreateDirectory(addonRoot);
        File.WriteAllText(Path.Combine(addonRoot, "plugin.gd"), "legacy");
        File.WriteAllText(Path.Combine(addonRoot, "plugin.gd.uid"), "uid://legacy123\n");
        GodotInstallOptions godotOptions = new(repairProjectSettings: true, enablePlugin: false);
        var options = InstallerInstallOptions.CreateGodotLocal(
            fixture.SourcePackageRoot,
            fixture.GodotProjectRoot,
            godotOptions,
            InstallerLegacyPackagePolicy.Reject);

        var plan = await new InstallerCoreWorkflowGateway().CreatePlanAsync(options, CancellationToken.None);

        Assert.Equal(InstallerTargetKind.Godot, plan.Engine);
        Assert.Contains(plan.Actions, action => action.Kind == InstallerPlanActionKind.InstallPackage);
        Assert.Contains(plan.Actions, action => action.TargetPath == fixture.GodotAddonRoot);
        Assert.Contains(plan.Actions, action => action.Kind == InstallerPlanActionKind.PatchProjectFile);
        Assert.Contains(plan.Actions, action => action.Kind == InstallerPlanActionKind.PatchProjectSettings);
        Assert.Equal(3, plan.Actions.Count);
        Assert.Contains(plan.Warnings, warning => warning.Contains("registration", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证关闭 project.godot 修复时预览不会宣称修改该文件。
    /// </summary>
    [Fact]
    public async Task CreatePlanAsyncOmitsGodotProjectSettingsActionWhenDisabled()
    {
        using var fixture = InstallerApplicationFixture.Create();
        GodotInstallOptions godotOptions = new(repairProjectSettings: false, enablePlugin: false);
        var options = InstallerInstallOptions.CreateGodotLocal(
            fixture.SourcePackageRoot,
            fixture.GodotProjectRoot,
            godotOptions,
            InstallerLegacyPackagePolicy.Reject);

        var plan = await new InstallerCoreWorkflowGateway().CreatePlanAsync(options, CancellationToken.None);

        Assert.DoesNotContain(plan.Actions, action => action.Kind == InstallerPlanActionKind.PatchProjectSettings);
        Assert.Contains(plan.Warnings, warning => warning.Contains("project.godot", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证 Godot 执行结果包含包和插件 owner 证据，并尊重关闭的 project.godot 修复选项。
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncMapsGodotResultWithoutDisabledProjectSettingsEvidence()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var projectSettingsPath = Path.Combine(fixture.GodotProjectRoot, "project.godot");
        var originalProjectSettings = File.ReadAllText(projectSettingsPath);
        GodotInstallOptions godotOptions = new(repairProjectSettings: false, enablePlugin: false);
        var options = InstallerInstallOptions.CreateGodotLocal(
            fixture.SourcePackageRoot,
            fixture.GodotProjectRoot,
            godotOptions,
            InstallerLegacyPackagePolicy.Reject);
        var gateway = new InstallerCoreWorkflowGateway();
        var plan = await gateway.CreatePlanAsync(options, CancellationToken.None);
        List<InstallerProgressStage> stages = new();

        var result = await gateway.ExecuteAsync(options, plan, new ProgressRecorder(stages), CancellationToken.None);

        Assert.Equal(fixture.GodotAddonRoot, result.TargetPath);
        Assert.True(result.Changed);
        Assert.Contains(result.EvidencePaths, path => path.EndsWith("YokiFrameGodotEditorPlugin.cs.uid", StringComparison.Ordinal));
        Assert.Contains(result.EvidencePaths, path => path.EndsWith("YokiFrameGodotBootstrap.cs", StringComparison.Ordinal));
        Assert.Contains(result.EvidencePaths, path => path.EndsWith("YokiFrameGodotBootstrap.cs.uid", StringComparison.Ordinal));
        Assert.DoesNotContain(projectSettingsPath, result.EvidencePaths);
        Assert.Equal(originalProjectSettings, File.ReadAllText(projectSettingsPath));
        Assert.Equal(new[] { InstallerProgressStage.Applying, InstallerProgressStage.Verifying }, stages);
    }

    /// <summary>
    /// 同步记录 gateway 上报的进度阶段。
    /// </summary>
    /// <param name="stages">接收阶段的列表。</param>
    private sealed class ProgressRecorder(List<InstallerProgressStage> stages) : IProgress<InstallerProgressUpdate>
    {
        /// <summary>
        /// 记录一次进度阶段。
        /// </summary>
        /// <param name="value">进度更新。</param>
        public void Report(InstallerProgressUpdate value)
        {
            stages.Add(value.Stage);
        }
    }
}
