using YokiFrame.Packaging.Services;
using YokiFrame.RuntimeCache;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Runtime manifest 与物理发布载荷的共享完整性门禁。
/// </summary>
public sealed class RuntimeManifestIntegrityValidatorTests
{
    private const string RUNTIME_PROFILE = "win-x64";

    /// <summary>
    /// 验证生产 Builder/Writer 生成的完整双入口 manifest 可直接复用。
    /// </summary>
    [Fact]
    public void ValidateAcceptsCompleteManifest()
    {
        var fixture = CreateFixture();

        var valid = Validate(fixture, out var profile, out var error);

        Assert.True(valid, error);
        Assert.Equal(fixture.GuiPath, profile.GuiPath);
        Assert.Equal(fixture.CliPath, profile.CliPath);
    }

    /// <summary>
    /// 验证入口仍存在但内容被等长篡改时，缓存不能被误判为可复用。
    /// </summary>
    [Fact]
    public void ValidateRejectsTamperedFileWithExistingEntry()
    {
        var fixture = CreateFixture();
        File.WriteAllText(fixture.GuiPath, "changed-gui");

        var valid = Validate(fixture, out _, out _);

        Assert.False(valid);
    }

    /// <summary>
    /// 验证平台目录的额外载荷与 manifest 声明文件缺失都会导致 cache miss。
    /// </summary>
    /// <param name="addUnexpectedFile">true 写入额外文件，false 删除已声明 CLI。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateRejectsPhysicalFileSetMismatch(bool addUnexpectedFile)
    {
        var fixture = CreateFixture();
        if (addUnexpectedFile)
        {
            File.WriteAllText(Path.Combine(fixture.PlatformRoot, "unexpected.dat"), "unexpected");
        }
        else
        {
            File.Delete(fixture.CliPath);
        }

        Assert.False(Validate(fixture, out _, out _));
    }

    /// <summary>
    /// 验证 manifest 生成后把入口替换为指向缓存外部的符号链接也会被拒绝。
    /// </summary>
    [Fact]
    public void ValidateRejectsEntrypointSymbolicLink()
    {
        var fixture = CreateFixture();
        var outsidePath = Path.Combine(fixture.Root, "outside-gui");
        File.WriteAllText(outsidePath, File.ReadAllText(fixture.GuiPath));
        File.Delete(fixture.GuiPath);
        try
        {
            File.CreateSymbolicLink(fixture.GuiPath, outsidePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        Assert.False(Validate(fixture, out _, out _));
    }

    /// <summary>
    /// 创建由生产 manifest 组件写出的最小双入口 Runtime 缓存。
    /// </summary>
    /// <returns>测试路径集合。</returns>
    private static RuntimeManifestFixture CreateFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-manifest-integrity-tests", Guid.NewGuid().ToString("N"));
        var platformRoot = Path.Combine(root, RUNTIME_PROFILE);
        var guiPath = Path.Combine(platformRoot, "YokiFrame.Workbench.Avalonia.exe");
        var cliPath = Path.Combine(platformRoot, "yoki.exe");
        Directory.CreateDirectory(platformRoot);
        File.WriteAllText(guiPath, "original-gui");
        File.WriteAllText(cliPath, "original-cli");
        var manifest = new RuntimeManifestBuilder().Build(
            root,
            "YokiFrameTool",
            RUNTIME_PROFILE,
            Path.GetFileName(guiPath),
            Path.GetFileName(cliPath));
        var manifestPath = Path.Combine(root, "tool-manifest.json");
        new RuntimeManifestWriter().Write(manifest, manifestPath);
        return new RuntimeManifestFixture(root, platformRoot, guiPath, cliPath, manifestPath);
    }

    /// <summary>
    /// 使用共享生产校验器检查 fixture 当前状态。
    /// </summary>
    /// <param name="fixture">测试路径集合。</param>
    /// <param name="profile">可信入口结果。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>缓存完整时返回 true。</returns>
    private static bool Validate(
        RuntimeManifestFixture fixture,
        out RuntimeManifestProfileValidation profile,
        out string error)
    {
        return RuntimeManifestIntegrityValidator.TryValidateProfile(
            fixture.ManifestPath,
            fixture.Root,
            RUNTIME_PROFILE,
            requireCli: true,
            out profile,
            out error);
    }

    /// <summary>
    /// 保存 Runtime 完整性测试涉及的稳定路径。
    /// </summary>
    /// <param name="Root">Runtime 根。</param>
    /// <param name="PlatformRoot">平台根。</param>
    /// <param name="GuiPath">GUI 入口。</param>
    /// <param name="CliPath">CLI 入口。</param>
    /// <param name="ManifestPath">manifest 路径。</param>
    private sealed record RuntimeManifestFixture(
        string Root,
        string PlatformRoot,
        string GuiPath,
        string CliPath,
        string ManifestPath);
}
