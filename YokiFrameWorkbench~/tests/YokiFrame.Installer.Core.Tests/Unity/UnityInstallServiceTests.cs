using System.Text.Json.Nodes;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;
using YokiFrame.Installer.Core.Tests.Transactions;

namespace YokiFrame.Installer.Core.Tests.Unity;

/// <summary>
/// 锁定 Unity embedded 与 Git URL 两种互斥安装来源的计划和执行契约。
/// </summary>
public sealed class UnityInstallServiceTests
{
    private const string PACKAGE_ID = "com.hinatayoki.yokiframe";
    private const string CURRENT_GIT_URL = "https://github.com/HinataYoki/YokiFrame.git#main";
    private const string LEGACY_GIT_URL = "https://github.com/HinataYoki/YokiFrame.git#legacy";

    /// <summary>
    /// 验证 embedded 安装通过文件级投影和受管事务提交，并登记 Unity 解析所需的本地 file 依赖。
    /// </summary>
    [Fact]
    public void ExecuteEmbeddedUsesFileProjectionTransactionAndRegistersLocalDependency()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        fixture.SetYokiFrameGitDependency(LEGACY_GIT_URL);
        UnityInstallRequest request = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);

        _ = new UnityInstallService().Execute(request);

        Assert.True(File.Exists(fixture.GetEmbeddedPath("package.json")));
        Assert.True(File.Exists(fixture.GetEmbeddedPath("Documentation~/Api/00-GettingStarted/Entrypoints.md")));
        Assert.True(File.Exists(fixture.GetEmbeddedPath("Documentation~/Guides/Tooling.md")));
        Assert.True(File.Exists(fixture.GetEmbeddedPath("Core/Runtime/Alpha.cs")));
        Assert.True(File.Exists(fixture.GetEmbeddedPath("Core/Runtime/Alpha.cs.meta")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("WorkbenchRuntime~/win-x64/YokiFrame.Workbench.Avalonia.exe")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("Core/Tests/AlphaTests.cs")));
        Assert.True(File.Exists(fixture.GetEmbeddedPath("YokiFrameWorkbench~/src/YokiFrame.Installer.Core/Installer.cs")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("YokiFrameWorkbench~/.artifacts-installer-ui/Installer.dll")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("YokiFrameWorkbench~/src/YokiFrame.Installer.Core/.artifacts-validation/Installer.dll")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("WorkbenchRuntime~/linux-x64/yoki")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("Tools/BuffKit/Runtime/BuffKit.cs")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("Tools/InputKit/Runtime/InputKit.cs")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("Documentation~/Architecture_Guardrails.md")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("Documentation~/README.md")));
        Assert.False(File.Exists(fixture.GetEmbeddedPath("Documentation~/Api/00-GettingStarted/Entrypoints.md.meta")));

        var ownerManifest = new PackageOwnerManifestStore().Read(fixture.EmbeddedPackageRoot);
        Assert.Contains(ownerManifest.Files, file => file.RelativePath == "Core/Runtime/Alpha.cs");
        Assert.Contains(ownerManifest.Files, file => file.RelativePath == "Core/Runtime/Alpha.cs.meta");
        Assert.DoesNotContain(ownerManifest.Files, file => file.RelativePath == ".yokiframe-owner.json");
        Assert.DoesNotContain(ownerManifest.Files, file => file.RelativePath.Contains("/Tests/", StringComparison.Ordinal));

        var manifest = fixture.ReadManifest();
        var dependencies = Assert.IsType<JsonObject>(manifest["dependencies"]);
        Assert.Equal(
            UnityManifestDependencyStore.EMBEDDED_PACKAGE_DEPENDENCY,
            dependencies[PACKAGE_ID]?.GetValue<string>());
        Assert.Equal("3.0.6", dependencies["com.unity.textmeshpro"]?.GetValue<string>());
        Assert.True(manifest["enableLockFile"]?.GetValue<bool>());
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证普通 Runtime 脚本更新不会重写依赖文本不变的 manifest，避免无意义地重新解析全部 Unity 包。
    /// </summary>
    [Fact]
    public void ExecuteEmbeddedKeepsUnchangedManifestAfterRuntimeCodeUpdate()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallRequest request = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);
        UnityInstallService service = new();
        _ = service.Execute(request);
        var manifestBefore = File.ReadAllBytes(fixture.ManifestPath);
        DateTime staleWriteTime = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(fixture.ManifestPath, staleWriteTime);
        File.WriteAllText(
            Path.Combine(fixture.SourcePackageRoot, "Core", "Runtime", "Alpha.cs"),
            "alpha-v2");

        var result = service.Execute(request);

        Assert.Equal("alpha-v2", File.ReadAllText(fixture.GetEmbeddedPath("Core/Runtime/Alpha.cs")));
        Assert.False(result.ManifestChanged);
        Assert.Equal(manifestBefore, File.ReadAllBytes(fixture.ManifestPath));
        Assert.Equal(staleWriteTime, File.GetLastWriteTimeUtc(fixture.ManifestPath));
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证已受管 embedded 包存在本地修改时，计划公开修改路径并由安装事务完整替换旧包。
    /// </summary>
    [Fact]
    public void ExecuteEmbeddedReplacesModifiedManagedPackage()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallRequest request = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);
        UnityInstallService service = new();
        _ = service.Execute(request);
        fixture.WriteEmbeddedFile("Core/Runtime/Alpha.cs", "manual-change");

        var plan = service.CreatePlan(request);
        var result = service.Execute(request);

        Assert.Equal(PackageOwnershipState.Modified, plan.ExistingPackageState);
        Assert.Contains("Core/Runtime/Alpha.cs", plan.ModifiedPaths);
        Assert.Equal(
            "Core/Runtime/Alpha.cs",
            File.ReadAllText(fixture.GetEmbeddedPath("Core/Runtime/Alpha.cs")));
        Assert.NotNull(result.PackageTransaction);
        Assert.True(result.PackageTransaction.ReplacedExistingPackage);
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证程序集定义变化仍会刷新相同 manifest 内容，使 Unity 重建 embedded 包的程序集图。
    /// </summary>
    [Fact]
    public void ExecuteEmbeddedRefreshesUnchangedManifestAfterAssemblyGraphUpdate()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallRequest request = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);
        UnityInstallService service = new();
        _ = service.Execute(request);
        var manifestBefore = File.ReadAllBytes(fixture.ManifestPath);
        DateTime staleWriteTime = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(fixture.ManifestPath, staleWriteTime);
        File.WriteAllText(
            Path.Combine(fixture.SourcePackageRoot, "Core", "Runtime", "YokiFrame.Runtime.asmdef"),
            "{\"name\":\"YokiFrame.Runtime\",\"references\":[]}");

        var result = service.Execute(request);

        Assert.False(result.ManifestChanged);
        Assert.Equal(manifestBefore, File.ReadAllBytes(fixture.ManifestPath));
        Assert.NotEqual(staleWriteTime, File.GetLastWriteTimeUtc(fixture.ManifestPath));
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证 Git 模式只结构化写入依赖，保留其它 JSON 内容，不创建 embedded 包且重复执行逐字节幂等。
    /// </summary>
    [Fact]
    public void ExecuteGitWritesStructuredAtomicIdempotentManifestWithoutEmbeddedCopy()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallRequest request = new(
            sourcePackageRoot: string.Empty,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.GitUrl,
            CURRENT_GIT_URL,
            UnmanagedPackagePolicy.Reject);
        UnityInstallService service = new();

        _ = service.Execute(request);
        var firstWrite = File.ReadAllBytes(fixture.ManifestPath);
        _ = service.Execute(request);

        var manifest = fixture.ReadManifest();
        var dependencies = Assert.IsType<JsonObject>(manifest["dependencies"]);
        Assert.Equal(CURRENT_GIT_URL, dependencies[PACKAGE_ID]?.GetValue<string>());
        Assert.Equal("3.0.6", dependencies["com.unity.textmeshpro"]?.GetValue<string>());
        Assert.Equal("highestMinor", manifest["resolutionStrategy"]?.GetValue<string>());
        Assert.Equal(firstWrite, File.ReadAllBytes(fixture.ManifestPath));
        Assert.False(Directory.Exists(fixture.EmbeddedPackageRoot));
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证 Git 依赖落盘后被外部篡改时，提交后重读会失败并逐字节恢复 manifest 与原 embedded 包。
    /// </summary>
    [Fact]
    public void ExecuteGitRollsBackWhenPersistedDependencyFailsPostVerification()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallService baselineService = new();
        UnityInstallRequest embeddedRequest = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);
        _ = baselineService.Execute(embeddedRequest);
        var manifestBefore = File.ReadAllBytes(fixture.ManifestPath);
        var packageBefore = DirectoryTreeSnapshot.Capture(fixture.EmbeddedPackageRoot);
        UnityInstallService service = new(new CallbackUnityInstallFaultInjector(checkpoint =>
        {
            if (checkpoint == UnityInstallCheckpoint.GitDependencyPersisted)
            {
                fixture.SetYokiFrameGitDependency(LEGACY_GIT_URL);
            }
        }));
        UnityInstallRequest gitRequest = new(
            sourcePackageRoot: string.Empty,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.GitUrl,
            CURRENT_GIT_URL,
            UnmanagedPackagePolicy.Reject);

        _ = Assert.Throws<InvalidDataException>(() => service.Execute(gitRequest));

        Assert.Equal(manifestBefore, File.ReadAllBytes(fixture.ManifestPath));
        packageBefore.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.EmbeddedPackageRoot));
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证 embedded 本地依赖落盘后被外部篡改时，提交后重读会失败并逐字节恢复 manifest 与旧受管包。
    /// </summary>
    [Fact]
    public void ExecuteEmbeddedRollsBackWhenPersistedDependencyFailsPostVerification()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallRequest embeddedRequest = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);
        UnityInstallService baselineService = new();
        _ = baselineService.Execute(embeddedRequest);
        fixture.SetYokiFrameGitDependency(LEGACY_GIT_URL);
        var manifestBefore = File.ReadAllBytes(fixture.ManifestPath);
        var packageBefore = DirectoryTreeSnapshot.Capture(fixture.EmbeddedPackageRoot);
        UnityInstallService service = new(new CallbackUnityInstallFaultInjector(checkpoint =>
        {
            if (checkpoint == UnityInstallCheckpoint.EmbeddedDependencyPersisted)
            {
                fixture.SetYokiFrameGitDependency(CURRENT_GIT_URL);
            }
        }));

        _ = Assert.Throws<InvalidDataException>(() => service.Execute(embeddedRequest));

        Assert.Equal(manifestBefore, File.ReadAllBytes(fixture.ManifestPath));
        packageBefore.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.EmbeddedPackageRoot));
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证 Installer 接受明确的 HTTPS、Git 与 file 绝对 URI，同时保留 query 和 fragment。
    /// </summary>
    /// <param name="gitUrl">应被接受的 Unity Git URL。</param>
    [Theory]
    [InlineData("https://github.com/HinataYoki/YokiFrame.git?path=Assets/YokiFrame#main")]
    [InlineData("git://github.com/HinataYoki/YokiFrame.git#main")]
    [InlineData("file:///F:/YokiFrame/.git#0123456789abcdef")]
    public void ValidateGitUrlAcceptsSupportedAbsoluteUris(string gitUrl)
    {
        UnityManifestDependencyStore.ValidateGitUrl(gitUrl);
    }

    /// <summary>
    /// 验证 Installer 在写 manifest 前拒绝相对路径、裸本地路径和未允许的 URI scheme。
    /// </summary>
    /// <param name="gitUrl">应被拒绝的依赖值。</param>
    [Theory]
    [InlineData("relative/YokiFrame.git")]
    [InlineData("F:\\YokiFrame\\.git")]
    [InlineData("http://github.com/HinataYoki/YokiFrame.git")]
    [InlineData("ftp://github.com/HinataYoki/YokiFrame.git")]
    public void ValidateGitUrlRejectsUnsupportedOrNonAbsoluteValues(string gitUrl)
    {
        _ = Assert.Throws<ArgumentException>(() => UnityManifestDependencyStore.ValidateGitUrl(gitUrl));
    }

    /// <summary>
    /// 验证同一项目两种来源并存时，计划明确列出删除旧来源和提交目标来源的互斥动作。
    /// </summary>
    [Fact]
    public void CreatePlanMakesExistingSourceConflictResolutionExplicit()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        fixture.WriteEmbeddedFile("legacy.marker", "legacy");
        fixture.SetYokiFrameGitDependency(LEGACY_GIT_URL);
        UnityInstallRequest embeddedRequest = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.TakeOverConfirmed);
        UnityInstallRequest gitRequest = new(
            sourcePackageRoot: string.Empty,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.GitUrl,
            CURRENT_GIT_URL,
            UnmanagedPackagePolicy.TakeOverConfirmed);
        UnityInstallService service = new();

        var embeddedKinds = service.CreatePlan(embeddedRequest).Actions.Select(static action => action.Kind).ToArray();
        var gitKinds = service.CreatePlan(gitRequest).Actions.Select(static action => action.Kind).ToArray();

        Assert.Contains(UnityInstallPlanActionKind.SetEmbeddedDependency, embeddedKinds);
        Assert.Contains(UnityInstallPlanActionKind.InstallEmbeddedPackage, embeddedKinds);
        Assert.Contains(UnityInstallPlanActionKind.RemoveEmbeddedPackage, gitKinds);
        Assert.Contains(UnityInstallPlanActionKind.SetGitDependency, gitKinds);
    }

    /// <summary>
    /// 验证受管 embedded 文件被修改后，Git 来源切换仍备份并移除旧包，再提交唯一 Git 依赖。
    /// </summary>
    [Fact]
    public void ExecuteGitReplacesModifiedEmbeddedPackage()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        UnityInstallRequest embeddedRequest = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);
        UnityInstallService service = new();
        _ = service.Execute(embeddedRequest);
        fixture.WriteEmbeddedFile("Core/Runtime/Alpha.cs", "manual-change");
        UnityInstallRequest gitRequest = new(
            sourcePackageRoot: string.Empty,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.GitUrl,
            CURRENT_GIT_URL,
            UnmanagedPackagePolicy.Reject);

        var plan = service.CreatePlan(gitRequest);
        var result = service.Execute(gitRequest);

        Assert.Equal(PackageOwnershipState.Modified, plan.ExistingPackageState);
        Assert.Contains("Core/Runtime/Alpha.cs", plan.ModifiedPaths);
        Assert.False(Directory.Exists(fixture.EmbeddedPackageRoot));
        Assert.True(result.ManifestChanged);
        var manifest = fixture.ReadManifest();
        var dependencies = Assert.IsType<JsonObject>(manifest["dependencies"]);
        Assert.Equal(CURRENT_GIT_URL, dependencies[PACKAGE_ID]?.GetValue<string>());
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证无效 Unity manifest 在结构化解析阶段失败，原文和目录内容均不发生变化。
    /// </summary>
    [Fact]
    public void ExecuteGitLeavesInvalidManifestUntouched()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        const string invalidManifest = "{ invalid-json";
        fixture.WriteManifestText(invalidManifest);
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);
        UnityInstallRequest request = new(
            sourcePackageRoot: string.Empty,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.GitUrl,
            CURRENT_GIT_URL,
            UnmanagedPackagePolicy.Reject);

        _ = Assert.Throws<InvalidDataException>(() => new UnityInstallService().Execute(request));

        Assert.Equal(invalidManifest, File.ReadAllText(fixture.ManifestPath));
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
        fixture.AssertNoManifestTemporaryFiles();
    }

    /// <summary>
    /// 验证计划入口沿用 TargetProjectDetector 的 Unity 2022.3 最低版本门控且拒绝阶段零写入。
    /// </summary>
    [Fact]
    public void CreatePlanReusesUnity2022_3VersionGate()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create("2021.3.45f1");
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);
        UnityInstallRequest request = new(
            fixture.SourcePackageRoot,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);

        var exception = Assert.Throws<InvalidDataException>(() => new UnityInstallService().CreatePlan(request));

        Assert.Contains("2022.3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("2021.3.45f1", exception.Message, StringComparison.Ordinal);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }
}
