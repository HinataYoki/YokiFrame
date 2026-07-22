using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using YokiFrame;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// 验证 Godot Workbench 启动器只启动完整、受控且匹配当前平台的 Runtime 缓存。
/// </summary>
public sealed class GodotWorkbenchLauncherTests
{
    private const string WINDOWS_NATIVE_AOT_RUNTIME_ID = "win-x64-aot";

    /// <summary>
    /// 验证 Windows 宿主只选择 Native AOT profile，不回退到开发期 managed profile。
    /// </summary>
    [Fact]
    public void ResolvePreferredRuntimeIdsUsesWindowsNativeAotProfile()
    {
        var runtimeIds = ResolvePreferredRuntimeIds("win-x64");

        Assert.Equal(new[] { WINDOWS_NATIVE_AOT_RUNTIME_ID }, runtimeIds);
    }

    /// <summary>
    /// 验证文件摘要、物理全集和双入口均有效时返回可信 GUI 路径。
    /// </summary>
    [Fact]
    public void ValidateAcceptsCompleteRuntimeManifest()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);

        var valid = Validate(fixture, out var executablePath, out var error);

        Assert.True(valid, error);
        Assert.Equal(fixture.GuiPath, executablePath);
    }

    /// <summary>
    /// 验证只存在 managed Windows profile 时不会被 Native AOT 启动策略接受。
    /// </summary>
    [Fact]
    public void ValidateRejectsWindowsManagedProfileWhenAotIsRequired()
    {
        using var fixture = new RuntimeManifestFixture("win-x64");

        var valid = Validate(fixture, out _, out _);

        Assert.False(valid);
    }

    /// <summary>
    /// 验证入口内容在 manifest 生成后被篡改时，即使文件仍存在也会拒绝启动。
    /// </summary>
    [Fact]
    public void ValidateRejectsTamperedEntrypoint()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        File.WriteAllText(fixture.GuiPath, "tampered-gui");

        var valid = Validate(fixture, out _, out _);

        Assert.False(valid);
    }

    /// <summary>
    /// 验证平台目录出现 manifest 未声明的额外发布载荷时拒绝启动。
    /// </summary>
    [Fact]
    public void ValidateRejectsUnexpectedRuntimePayload()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        File.WriteAllText(Path.Combine(fixture.PlatformRoot, "unexpected.dat"), "unexpected");

        var valid = Validate(fixture, out _, out _);

        Assert.False(valid);
    }

    /// <summary>
    /// 验证其它字段与文件摘要均有效时，单独把 GUI entry 改为上级目录仍会命中越界门禁。
    /// </summary>
    [Fact]
    public void ValidateRejectsRuntimeEntrypointTraversal()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        var outsidePath = Path.Combine(
            Path.GetDirectoryName(fixture.Root)!,
            "outside-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(outsidePath, "outside");
        try
        {
            var escapedEntry = Path.GetRelativePath(fixture.Root, outsidePath).Replace('\\', '/');
            fixture.WriteManifest(platform => platform with { GuiEntry = escapedEntry });

            var valid = Validate(fixture, out _, out _);

            Assert.False(valid);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    /// <summary>
    /// 验证目标 profile 重复时拒绝选择第一条记录，避免歧义 manifest 绕过校验。
    /// </summary>
    [Fact]
    public void ValidateRejectsDuplicateRuntimeProfile()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        fixture.WriteManifest(duplicatePlatform: true);

        var valid = Validate(fixture, out _, out _);

        Assert.False(valid);
    }

    /// <summary>
    /// 验证 platform 与 runtimeIdentifier 不一致时拒绝缓存。
    /// </summary>
    [Fact]
    public void ValidateRejectsMismatchedRuntimeIdentifier()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        fixture.WriteManifest(platform => platform with { RuntimeIdentifier = "linux-x64" });

        var valid = Validate(fixture, out _, out _);

        Assert.False(valid);
    }

    /// <summary>
    /// 验证已列入 manifest 的入口被替换为缓存外符号链接时拒绝启动。
    /// </summary>
    [Fact]
    public void ValidateRejectsEntrypointSymbolicLink()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        var outsidePath = Path.Combine(
            Path.GetDirectoryName(fixture.Root)!,
            "outside-gui-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(outsidePath, File.ReadAllText(fixture.GuiPath));
        File.Delete(fixture.GuiPath);
        try
        {
            File.CreateSymbolicLink(fixture.GuiPath, outsidePath);
        }
        catch (Exception exception) when (IsSymbolicLinkUnavailable(exception))
        {
            File.Delete(outsidePath);
            return;
        }

        try
        {
            var valid = Validate(fixture, out _, out _);

            Assert.False(valid);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    /// <summary>
    /// 验证 manifest 文件自身被替换为链接时拒绝读取链接目标。
    /// </summary>
    [Fact]
    public void ValidateRejectsManifestSymbolicLink()
    {
        using var fixture = new RuntimeManifestFixture(WINDOWS_NATIVE_AOT_RUNTIME_ID);
        var outsidePath = Path.Combine(
            Path.GetDirectoryName(fixture.Root)!,
            "outside-manifest-" + Guid.NewGuid().ToString("N") + ".json");
        File.Move(fixture.ManifestPath, outsidePath);
        try
        {
            File.CreateSymbolicLink(fixture.ManifestPath, outsidePath);
        }
        catch (Exception exception) when (IsSymbolicLinkUnavailable(exception))
        {
            File.Move(outsidePath, fixture.ManifestPath);
            return;
        }

        try
        {
            var valid = Validate(fixture, out _, out _);

            Assert.False(valid);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    /// <summary>
    /// 验证项目 `.yokiframe` 目录整体指向项目外时，current 指针和 Runtime 均不能被接受。
    /// </summary>
    [Fact]
    public void ResolveRuntimeRootRejectsExternalStateDirectorySymbolicLink()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-godot-launcher-path-tests",
            Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(testRoot, "project");
        var outsideStateRoot = Path.Combine(testRoot, "outside-state");
        var linkPath = Path.Combine(projectRoot, ".yokiframe");
        Directory.CreateDirectory(projectRoot);
        CreateExternalRuntimePointer(outsideStateRoot);
        try
        {
            Directory.CreateSymbolicLink(linkPath, outsideStateRoot);
        }
        catch (Exception exception) when (IsSymbolicLinkUnavailable(exception))
        {
            Directory.Delete(testRoot, recursive: true);
            return;
        }

        try
        {
            var exception = Assert.Throws<TargetInvocationException>(() => ResolveRuntimeRoot(projectRoot));
            Assert.IsType<InvalidDataException>(exception.InnerException);
        }
        finally
        {
            Directory.Delete(linkPath);
            Directory.Delete(testRoot, recursive: true);
        }
    }

    /// <summary>
    /// 反射调用生产代码的候选 RID 解析，避免测试复制 Windows 发布策略。
    /// </summary>
    /// <param name="runtimeId">当前宿主基础 RID。</param>
    /// <returns>按优先级排列的 Runtime profile 标识。</returns>
    private static string[] ResolvePreferredRuntimeIds(string runtimeId)
    {
        var method = GetLauncherMethod("ResolvePreferredRuntimeIds");
        return Assert.IsType<string[]>(method.Invoke(null, new object[] { runtimeId }));
    }

    /// <summary>
    /// 反射调用生产 Runtime 根解析，保留异常包装供测试判断具体拒绝原因类型。
    /// </summary>
    /// <param name="projectRoot">测试项目根。</param>
    /// <returns>解析成功后的 Runtime 根。</returns>
    private static string ResolveRuntimeRoot(string projectRoot)
    {
        var method = GetLauncherMethod("ResolveRuntimeRoot");
        return Assert.IsType<string>(method.Invoke(null, new object[] { projectRoot }));
    }

    /// <summary>
    /// 反射调用生产完整性校验器，并读取其两个输出参数。
    /// </summary>
    /// <param name="fixture">Runtime 缓存夹具。</param>
    /// <param name="executablePath">校验成功后的 GUI 入口。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>缓存完整可信时返回 true。</returns>
    private static bool Validate(
        RuntimeManifestFixture fixture,
        out string executablePath,
        out string error)
    {
        var method = GetLauncherMethod("TryValidateRuntimeManifest");
        object[] arguments =
        {
            fixture.ManifestPath,
            fixture.Root,
            ResolvePreferredRuntimeIds("win-x64"),
            string.Empty,
            string.Empty
        };
        var valid = Assert.IsType<bool>(method.Invoke(null, arguments));
        executablePath = Assert.IsType<string>(arguments[3]);
        error = Assert.IsType<string>(arguments[4]);
        return valid;
    }

    /// <summary>
    /// 判断当前环境是否不允许创建测试符号链接；仅在该能力不可用时跳过该断言。
    /// </summary>
    /// <param name="exception">创建符号链接时抛出的异常。</param>
    /// <returns>属于环境能力限制时返回 true。</returns>
    private static bool IsSymbolicLinkUnavailable(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException;
    }

    /// <summary>
    /// 在项目外创建 current 指针及其指向的最小 Runtime 目录，用于祖先链接拒绝测试。
    /// </summary>
    /// <param name="outsideStateRoot">项目外状态目录。</param>
    private static void CreateExternalRuntimePointer(string outsideStateRoot)
    {
        const string fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var cacheRoot = Path.Combine(outsideStateRoot, "runtime", "com.hinatayoki.yokiframe");
        Directory.CreateDirectory(Path.Combine(cacheRoot, fingerprint));
        var pointer = new { layoutVersion = 1, sourceFingerprint = fingerprint };
        File.WriteAllText(
            Path.Combine(cacheRoot, "current.json"),
            JsonSerializer.Serialize(pointer));
    }

    /// <summary>
    /// 获取指定私有启动器方法，类型或方法缺失时让测试清晰失败。
    /// </summary>
    /// <param name="methodName">目标私有静态方法名。</param>
    /// <returns>用于测试调用的方法元数据。</returns>
    private static MethodInfo GetLauncherMethod(string methodName)
    {
        var launcherType = typeof(GodotEditorFileBridgeHost).Assembly.GetType("YokiFrame.GodotWorkbenchLauncher")
            ?? throw new InvalidOperationException("Godot Workbench launcher type is missing.");
        return launcherType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Godot Workbench launcher method is missing: " + methodName);
    }

    /// <summary>
    /// 创建可独立变更 manifest 或物理载荷的最小双入口 Runtime 缓存。
    /// </summary>
    private sealed class RuntimeManifestFixture : IDisposable
    {
        private static readonly JsonSerializerOptions sJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// 创建指定 profile 的完整 Runtime 缓存并写入初始 manifest。
        /// </summary>
        /// <param name="runtimeProfile">平台 profile。</param>
        internal RuntimeManifestFixture(string runtimeProfile)
        {
            RuntimeProfile = runtimeProfile;
            Root = Path.Combine(
                Path.GetTempPath(),
                "yokiframe-godot-launcher-tests",
                Guid.NewGuid().ToString("N"));
            PlatformRoot = Path.Combine(Root, runtimeProfile);
            GuiPath = Path.Combine(PlatformRoot, "YokiFrame.Workbench.Avalonia.exe");
            CliPath = Path.Combine(PlatformRoot, "yoki.exe");
            ManifestPath = Path.Combine(Root, "tool-manifest.json");
            Directory.CreateDirectory(PlatformRoot);
            File.WriteAllText(GuiPath, "original-gui");
            File.WriteAllText(CliPath, "original-cli");
            WriteManifest();
        }

        internal string RuntimeProfile { get; }

        internal string Root { get; }

        internal string PlatformRoot { get; }

        internal string GuiPath { get; }

        internal string CliPath { get; }

        internal string ManifestPath { get; }

        /// <summary>
        /// 重新生成文件摘要有效的 manifest，并可单独变更目标平台记录。
        /// </summary>
        /// <param name="mutate">平台记录变更函数。</param>
        /// <param name="duplicatePlatform">是否写入重复目标 profile。</param>
        internal void WriteManifest(
            Func<RuntimePlatformModel, RuntimePlatformModel>? mutate = null,
            bool duplicatePlatform = false)
        {
            var platform = CreatePlatformModel();
            platform = mutate == null ? platform : mutate(platform);
            var platforms = duplicatePlatform
                ? new[] { platform, platform }
                : new[] { platform };
            var manifest = new RuntimeManifestModel(1, 2, ".", platforms);
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, sJsonOptions));
        }

        /// <summary>
        /// 删除当前夹具创建的临时 Runtime 根。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        /// <summary>
        /// 根据当前物理文件生成包含大小与 SHA-256 的平台记录。
        /// </summary>
        /// <returns>完整平台记录。</returns>
        private RuntimePlatformModel CreatePlatformModel()
        {
            var gui = CreateFileModel(GuiPath);
            var cli = CreateFileModel(CliPath);
            var guiEntry = gui.RelativePath;
            return new RuntimePlatformModel(
                RuntimeProfile,
                RuntimeProfile,
                guiEntry,
                guiEntry,
                cli.RelativePath,
                2,
                gui.SizeBytes + cli.SizeBytes,
                new[] { gui, cli });
        }

        /// <summary>
        /// 为物理文件创建 Runtime 根相对路径、长度和摘要记录。
        /// </summary>
        /// <param name="path">目标文件。</param>
        /// <returns>manifest 文件记录。</returns>
        private RuntimeFileModel CreateFileModel(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var relativePath = Path.GetRelativePath(Root, path).Replace('\\', '/');
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return new RuntimeFileModel(relativePath, bytes.LongLength, sha256);
        }
    }

    /// <summary>
    /// 表示测试所需的最小 Runtime manifest 头。
    /// </summary>
    private sealed record RuntimeManifestModel(
        int ManifestVersion,
        int LayoutVersion,
        string RuntimeRoot,
        RuntimePlatformModel[] Platforms);

    /// <summary>
    /// 表示测试所需的单个平台 Runtime 发布记录。
    /// </summary>
    private sealed record RuntimePlatformModel(
        string Platform,
        string RuntimeIdentifier,
        string Entrypoint,
        string GuiEntry,
        string CliEntry,
        int FileCount,
        long TotalBytes,
        RuntimeFileModel[] Files);

    /// <summary>
    /// 表示测试 manifest 中单个发布文件的长度和摘要。
    /// </summary>
    private sealed record RuntimeFileModel(string RelativePath, long SizeBytes, string Sha256);
}
