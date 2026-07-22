using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Client;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.ProjectModel;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 验证 Project Model owner 从项目事实生成 bundle、维护 harness projection，并拒绝来源漂移。
/// </summary>
public sealed class ProjectModelServiceTests
{
    /// <summary>
    /// 验证没有旧 harness 时首次 refresh 生成 bundle，第二次相同输入保持 changed=false。
    /// </summary>
    [Fact]
    public void FirstRefreshCreatesHarnessAndSecondRefreshIsIdempotent()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var service = fixture.CreateService();

        Assert.False(File.Exists(fixture.HarnessPath));
        var first = service.Refresh();
        var second = service.Refresh();

        Assert.True(first.Changed);
        Assert.Equal("Ready", first.State);
        Assert.False(second.Changed);
        Assert.Equal(first.Bundle!.Manifest.ModelId, second.Bundle!.Manifest.ModelId);
        Assert.True(File.Exists(fixture.HarnessPath));
    }

    /// <summary>
    /// 验证静态 harness 从项目 `.yokiframe` 缓存读取当前 GUI 与 CLI，不回退到包内 Runtime 目录。
    /// </summary>
    [Fact]
    public void RefreshPublishesProjectRuntimeCachePaths()
    {
        using var fixture = ProjectModelTestFixture.Create();

        _ = fixture.CreateService().Refresh();

        var harness = JsonNode.Parse(File.ReadAllText(fixture.HarnessPath))!.AsObject();
        var cli = harness["cli"]!.AsObject();
        var workbench = harness["workbench"]!.AsObject();
        Assert.True(cli["available"]!.GetValue<bool>());
        Assert.True(workbench["available"]!.GetValue<bool>());
        Assert.StartsWith(".yokiframe/runtime/com.hinatayoki.yokiframe/", cli["runtimeRoot"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchRuntime~", cli["path"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证项目 Runtime 入口被篡改后，Project Model 不再发布可启动状态。
    /// </summary>
    [Fact]
    public void RefreshRejectsTamperedProjectRuntimeCache()
    {
        using var fixture = ProjectModelTestFixture.Create();
        fixture.TamperRuntimeGui();

        _ = fixture.CreateService().Refresh();

        var harness = JsonNode.Parse(File.ReadAllText(fixture.HarnessPath))!.AsObject();
        Assert.False(harness["cli"]!["available"]!.GetValue<bool>());
        Assert.False(harness["workbench"]!["available"]!.GetValue<bool>());
    }

    /// <summary>
    /// 验证 Project Model 使用 DependencyDefineService 的真实软依赖宏，供后续 Workbench 准确展示。
    /// </summary>
    [Fact]
    public void RefreshPublishesManagedOptionalDependencyDefines()
    {
        using var fixture = ProjectModelTestFixture.Create();
        fixture.SetPackageManifest(
            "{\"dependencies\":{\"com.cysharp.unitask\":\"2.5.10\",\"com.tuyoogame.yooasset\":\"3.0.2\"}}");

        var result = fixture.CreateService().Refresh();
        var integrations = result.Bundle!.Dependencies.OptionalIntegrations
            .ToDictionary(static integration => integration.Id, StringComparer.Ordinal);

        Assert.Equal("Detected", integrations["UniTask"].State);
        Assert.Equal("YOKIFRAME_UNITASK_SUPPORT", integrations["UniTask"].Define);
        Assert.Equal("Detected", integrations["YooAsset"].State);
        Assert.Equal("YOKIFRAME_YOOASSET_SUPPORT", integrations["YooAsset"].Define);
        Assert.Equal("YOKIFRAME_DOTWEEN_SUPPORT", integrations["DOTween"].Define);
        Assert.False(integrations.ContainsKey("FMOD"));
        Assert.Equal("YOKIFRAME_LUBAN_SUPPORT", integrations["Luban"].Define);
        Assert.Equal("YOKIFRAME_NINO_SUPPORT", integrations["Nino"].Define);
        Assert.Equal("YOKIFRAME_ZSTRING_SUPPORT", integrations["ZString"].Define);
        Assert.Equal("YOKIFRAME_INPUTSYSTEM_SUPPORT", integrations["InputSystem"].Define);
    }

    /// <summary>
    /// 验证相同输入下 refresh 会修复被外部篡改的 harness modelId，而不重新生成 Project Model。
    /// </summary>
    [Fact]
    public void RefreshRepairsTamperedHarnessModelIdWithoutChangingModel()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var service = fixture.CreateService();
        var first = service.Refresh();
        var harness = JsonNode.Parse(File.ReadAllText(fixture.HarnessPath))!.AsObject();
        harness["projectModel"]!["modelId"] = "tampered-model-id";
        File.WriteAllText(fixture.HarnessPath, harness.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var repaired = service.Refresh();
        var restored = JsonNode.Parse(File.ReadAllText(fixture.HarnessPath))!;

        Assert.False(repaired.Changed);
        Assert.Equal(first.Bundle!.Manifest.ModelId, repaired.Bundle!.Manifest.ModelId);
        Assert.Equal(first.Bundle.Manifest.ModelId, restored["projectModel"]!["modelId"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 --package 指向项目内错误包时返回稳定的包身份错误，而不会回退到其它候选包。
    /// </summary>
    [Fact]
    public void RefreshRejectsWrongPackageIdentityHintInsideProject()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var service = fixture.CreateService();
        var exception = Assert.Throws<YokiFrameProtocolException>(() => service.Refresh(fixture.CreateWrongPackage()));

        Assert.Equal("ProjectPackageIdentityMismatch", exception.Error.Code);
    }

    /// <summary>
    /// 验证实现源码变化会使 Inspect 离开 Ready，并在 refresh 时报告 descriptor sourceHash 漂移。
    /// </summary>
    [Fact]
    public void SourceChangeMakesInspectStaleAndRefreshReportsCapabilityHashMismatch()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var sourcePath = fixture.AddCapabilityDescriptor();
        var service = fixture.CreateService();
        _ = service.Refresh();
        File.WriteAllText(sourcePath, "namespace Fixture; public static class TestCapabilitySource { public const string Value = \"changed\"; }");

        var inspection = service.Inspect();
        var exception = Assert.Throws<YokiFrameProtocolException>(() => service.Refresh());

        Assert.NotEqual("Ready", inspection.State);
        Assert.Contains(inspection.Issues, issue => issue.Code == "ProjectModelStale");
        Assert.Equal("CapabilitySourceHashMismatch", exception.Error.Code);
    }

    /// <summary>
    /// 验证包内目录链接不能把 capability descriptor 扫描或实现源码哈希读取重定向到包外。
    /// </summary>
    [Fact]
    public void RefreshRejectsCapabilitySourceReparsePoint()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var linkPath = fixture.AddLinkedCapabilityDescriptor();
        if (linkPath == null)
        {
            return;
        }

        try
        {
            var exception = Assert.Throws<YokiFrameProtocolException>(() => fixture.CreateService().Refresh());

            Assert.Contains(
                exception.Error.Code,
                new[] { "ProjectPathReparsePoint", "CapabilitySourceReparsePoint" });
        }
        finally
        {
            Directory.Delete(linkPath);
        }
    }

    /// <summary>
    /// 验证 architecture ownership 使用项目相对包根，不把开发机绝对路径写入模型。
    /// </summary>
    [Fact]
    public void ArchitectureOwnershipUsesProjectRelativePackageRoot()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var result = fixture.CreateService().Refresh();
        var coreOwnership = result.Bundle!.Architecture.Ownership.Single(item => item.Owner == "YokiFrame" && item.Path.EndsWith("/Core/Runtime", StringComparison.Ordinal));

        Assert.Equal("Assets/YokiFrame/Core/Runtime", coreOwnership.Path);
        Assert.All(result.Bundle.Architecture.Ownership, item => Assert.False(Path.IsPathRooted(item.Path)));
    }

    /// <summary>
    /// 验证 ownership 与 manifest leaf hash 被同步篡改时仍会阻断 Ready，并由 refresh 提交新的可信 generation。
    /// </summary>
    [Fact]
    public void InspectBlocksSynchronizedOwnershipTamperAndRefreshRebuildsGeneration()
    {
        using var fixture = ProjectModelTestFixture.Create();
        var service = fixture.CreateService();
        var original = service.Refresh();
        fixture.TamperOwnershipAndRepairManifestHash();

        var inspection = service.Inspect();

        Assert.Equal("Blocked", inspection.State);
        Assert.Contains(inspection.Issues, issue => issue.Code == "ProjectModelGeneratedContentMismatch");

        var repaired = service.Refresh();

        Assert.True(repaired.Changed);
        Assert.NotEqual(original.Bundle!.Manifest.ModelGeneration, repaired.Bundle!.Manifest.ModelGeneration);
        Assert.NotEqual(original.Bundle.Manifest.ModelId, repaired.Bundle.Manifest.ModelId);
        Assert.Equal("Ready", service.Inspect().State);
        Assert.DoesNotContain(repaired.Bundle.Architecture.Ownership, item => item.Owner == "Attacker");
    }

    /// <summary>
    /// 构造 Project Model 测试使用的隔离 Unity 项目和开发包。
    /// </summary>
    private sealed class ProjectModelTestFixture : IDisposable
    {
        private const string PACKAGE_NAME = "com.hinatayoki.yokiframe";

        /// <summary>
        /// 初始化项目、包清单和 Unity 最低版本事实文件。
        /// </summary>
        private ProjectModelTestFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "yokiframe-project-model-tests", Guid.NewGuid().ToString("N"));
            ProjectRoot = Path.Combine(Root, "unity-project");
            PackageRoot = Path.Combine(ProjectRoot, "Assets", "YokiFrame");
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Packages"));
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "ProjectSettings"));
            Directory.CreateDirectory(Path.Combine(PackageRoot, "Core", "Runtime"));
            Directory.CreateDirectory(Path.Combine(PackageRoot, "Core", "Editor"));
            Directory.CreateDirectory(Path.Combine(PackageRoot, "Tools"));
            WriteText(Path.Combine(PackageRoot, "package.json"), "{\"name\":\"" + PACKAGE_NAME + "\",\"version\":\"2.0.0-test\"}");
            WriteText(Path.Combine(PackageRoot, "YokiFrameWorkbench~", "src", "FixtureBuildInput.cs"), "namespace Fixture; public sealed class FixtureBuildInput { }");
            WriteText(Path.Combine(ProjectRoot, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.0f1\n");
            WriteText(Path.Combine(ProjectRoot, "Packages", "manifest.json"), "{\"dependencies\":{\"com.unity.textmeshpro\":\"3.0.6\"}}");
            WriteRuntimeCache();
        }

        /// <summary>获取测试总根目录。</summary>
        private string Root { get; }

        /// <summary>获取 Unity 项目根目录。</summary>
        private string ProjectRoot { get; }

        /// <summary>获取项目内开发包根目录。</summary>
        private string PackageRoot { get; }

        /// <summary>获取静态 harness projection 路径。</summary>
        internal string HarnessPath => Path.Combine(ProjectRoot, ".yokiframe", "harness", "capabilities.json");

        /// <summary>创建测试现场。</summary>
        /// <returns>新的隔离 fixture。</returns>
        internal static ProjectModelTestFixture Create() => new();

        /// <summary>为当前项目创建 Project Model 应用服务。</summary>
        /// <returns>绑定当前临时项目的服务。</returns>
        internal ProjectModelService CreateService() => new(new YokiFrameClient(ProjectRoot));

        /// <summary>替换当前测试项目的 UPM manifest，用于验证可选依赖投影。</summary>
        /// <param name="content">完整 manifest JSON。</param>
        internal void SetPackageManifest(string content)
        {
            WriteText(Path.Combine(ProjectRoot, "Packages", "manifest.json"), content);
        }

        /// <summary>篡改已发布 GUI 入口，保留旧 manifest 摘要以验证完整性拒绝路径。</summary>
        internal void TamperRuntimeGui()
        {
            var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(PackageRoot);
            var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(ProjectRoot, sourceFingerprint);
            var runtimeProfile = ResolveRuntimeProfile();
            var guiName = runtimeProfile.StartsWith("win-", StringComparison.Ordinal)
                ? "YokiFrame.Workbench.Avalonia.exe"
                : "YokiFrame.Workbench.Avalonia";
            File.AppendAllText(Path.Combine(runtimeRoot, runtimeProfile, guiName), "tampered");
        }

        /// <summary>
        /// 写入一个带正确 sourceHash 的最小 capability descriptor，并返回其实现源码路径。
        /// </summary>
        /// <returns>descriptor 指向的实现源码绝对路径。</returns>
        internal string AddCapabilityDescriptor()
        {
            const string sourceRelativePath = "Core/Runtime/TestKit/TestCapabilitySource.cs";
            var sourcePath = Path.Combine(PackageRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceText = "namespace Fixture; public static class TestCapabilitySource { public const string Value = \"initial\"; }";
            WriteText(sourcePath, sourceText);
            WriteCapabilityDescriptor("TestKit", sourceRelativePath, sourceText);
            return sourcePath;
        }

        /// <summary>
        /// 创建指向包外实现目录的 capability 来源链接；当前宿主不允许链接时返回 null。
        /// </summary>
        /// <returns>创建成功时返回待清理链接路径，否则返回 null。</returns>
        internal string? AddLinkedCapabilityDescriptor()
        {
            const string sourceRelativePath = "Core/Runtime/LinkedSource/OutsideCapabilitySource.cs";
            var outsideRoot = Path.Combine(ProjectRoot, "ExternalCapability");
            var sourceText = "namespace Fixture; public static class OutsideCapabilitySource { }";
            WriteText(Path.Combine(outsideRoot, "OutsideCapabilitySource.cs"), sourceText);
            var linkPath = Path.Combine(PackageRoot, "Core", "Runtime", "LinkedSource");
            if (!TryCreateDirectoryLink(linkPath, outsideRoot))
            {
                return null;
            }

            WriteCapabilityDescriptor("LinkedKit", sourceRelativePath, sourceText);
            return linkPath;
        }

        /// <summary>
        /// 写入带确定性实现哈希的最小 capability descriptor。
        /// </summary>
        /// <param name="kit">descriptor Kit 标识。</param>
        /// <param name="sourceRelativePath">包内实现来源路径。</param>
        /// <param name="sourceText">实现源码正文。</param>
        private void WriteCapabilityDescriptor(string kit, string sourceRelativePath, string sourceText)
        {
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText))).ToLowerInvariant();
            var descriptor = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kind"] = "capability-descriptor",
                ["kit"] = new JsonObject
                {
                    ["kit"] = kit,
                    ["state"] = "Available",
                    ["role"] = "Core",
                    ["snapshotNames"] = new JsonArray(),
                    ["telemetryNames"] = new JsonArray(),
                    ["commandCatalogDeclared"] = false,
                    ["commands"] = new JsonArray(),
                    ["verifyRecipes"] = new JsonArray(),
                    ["sourcePath"] = sourceRelativePath,
                    ["sourceHash"] = sourceHash
                }
            };
            WriteText(Path.Combine(PackageRoot, "Core", "Runtime", kit, "capability.json"), descriptor.ToJsonString());
        }

        /// <summary>
        /// 写入当前宿主 profile 的最小项目 Runtime 缓存，模拟源码 bootstrap 成功后的稳定状态。
        /// </summary>
        private void WriteRuntimeCache()
        {
            var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(PackageRoot);
            var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(ProjectRoot, sourceFingerprint);
            var runtimeProfile = ResolveRuntimeProfile();
            var guiEntry = runtimeProfile.StartsWith("win-", StringComparison.Ordinal)
                ? runtimeProfile + "/YokiFrame.Workbench.Avalonia.exe"
                : runtimeProfile + "/YokiFrame.Workbench.Avalonia";
            var cliEntry = runtimeProfile.StartsWith("win-", StringComparison.Ordinal)
                ? runtimeProfile + "/yoki.exe"
                : runtimeProfile + "/yoki";
            WriteText(Path.Combine(runtimeRoot, guiEntry.Replace('/', Path.DirectorySeparatorChar)), "gui");
            WriteText(Path.Combine(runtimeRoot, cliEntry.Replace('/', Path.DirectorySeparatorChar)), "cli");
            var guiRecord = CreateRuntimeFileRecord(runtimeRoot, guiEntry);
            var cliRecord = CreateRuntimeFileRecord(runtimeRoot, cliEntry);
            var manifest = new JsonObject
            {
                ["manifestVersion"] = 1,
                ["layoutVersion"] = 2,
                ["runtimeRoot"] = ".",
                ["platforms"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["platform"] = runtimeProfile,
                        ["runtimeIdentifier"] = runtimeProfile,
                        ["guiEntry"] = guiEntry,
                        ["cliEntry"] = cliEntry,
                        ["fileCount"] = 2,
                        ["totalBytes"] = guiRecord["sizeBytes"]!.GetValue<long>()
                            + cliRecord["sizeBytes"]!.GetValue<long>(),
                        ["files"] = new JsonArray(guiRecord, cliRecord)
                    }
                }
            };
            WriteText(
                Path.Combine(runtimeRoot, "tool-manifest.json"),
                manifest.ToJsonString());
            WriteText(
                YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(ProjectRoot),
                "{\"layoutVersion\":1,\"sourceFingerprint\":\"" + sourceFingerprint + "\"}");
        }

        /// <summary>创建与实际 Runtime 文件一致的 manifest 摘要记录。</summary>
        private static JsonObject CreateRuntimeFileRecord(string runtimeRoot, string relativePath)
        {
            var fullPath = Path.Combine(runtimeRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return new JsonObject
            {
                ["relativePath"] = relativePath,
                ["sizeBytes"] = new FileInfo(fullPath).Length,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant()
            };
        }

        /// <summary>
        /// 根据测试进程宿主计算与应用层相同的 Runtime profile。
        /// </summary>
        /// <returns>当前平台受支持 profile。</returns>
        private static string ResolveRuntimeProfile()
        {
            if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                return "win-x64-aot";
            }

            if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                return "linux-x64";
            }

            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }

        /// <summary>
        /// 篡改 architecture owner 并同步更新 manifest contentHash，模拟能通过 Client leaf 完整性检查的伪造 bundle。
        /// </summary>
        internal void TamperOwnershipAndRepairManifestHash()
        {
            var projectModelRoot = Path.Combine(ProjectRoot, ".yokiframe", "project");
            var architecturePath = Path.Combine(projectModelRoot, "architecture.json");
            var architecture = JsonNode.Parse(File.ReadAllText(architecturePath))!.AsObject();
            var ownershipRule = architecture["ownership"]!.AsArray()
                .Select(static node => node!.AsObject())
                .Single(rule => rule["path"]!.GetValue<string>() == "Assets/**");
            ownershipRule["owner"] = "Attacker";
            var architectureJson = architecture.ToJsonString();
            WriteText(architecturePath, architectureJson);

            var architectureHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(architectureJson))).ToLowerInvariant();
            var manifestPath = Path.Combine(projectModelRoot, "project-model.json");
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            var architectureReference = manifest["documents"]!.AsArray()
                .Select(static node => node!.AsObject())
                .Single(reference => reference["path"]!.GetValue<string>() == "architecture.json");
            architectureReference["contentHash"] = architectureHash;
            WriteText(manifestPath, manifest.ToJsonString());
        }

        /// <summary>创建项目内 package.json 身份错误的候选包并返回其根目录。</summary>
        /// <returns>错误包根目录。</returns>
        internal string CreateWrongPackage()
        {
            var wrongRoot = Path.Combine(ProjectRoot, "Assets", "WrongPackage");
            WriteText(Path.Combine(wrongRoot, "package.json"), "{\"name\":\"com.example.wrong\",\"version\":\"1.0.0\"}");
            return wrongRoot;
        }

        /// <summary>删除测试现场，避免临时项目污染工作区。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        /// <summary>写入文本文件并创建其父目录。</summary>
        /// <param name="path">目标路径。</param>
        /// <param name="content">文件内容。</param>
        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        /// <summary>尝试创建目录符号链接；当前宿主不支持或权限不足时跳过专项断言。</summary>
        private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
                return true;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                                              or PlatformNotSupportedException
                                              or NotSupportedException
                                              or IOException)
            {
                return false;
            }
        }
    }
}
