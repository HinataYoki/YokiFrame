using System.Runtime.InteropServices;
using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖当前宿主平台到项目级 Workbench Runtime profile 的映射。
/// </summary>
public sealed class RuntimePublishProfileResolverTests
{
    /// <summary>
    /// 获取当前首批支持的 Windows、Linux 与 macOS profile 期望值。
    /// </summary>
    public static TheoryData<OSPlatform, Architecture, string, string, string, string, string> SupportedProfiles => new()
    {
        {
            OSPlatform.Windows,
            Architecture.X64,
            "win-x64-aot",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            "yoki.exe",
            string.Empty
        },
        {
            OSPlatform.Linux,
            Architecture.X64,
            "linux-x64",
            "linux-x64",
            "YokiFrame.Workbench.Avalonia",
            "yoki",
            string.Empty
        },
        {
            OSPlatform.OSX,
            Architecture.X64,
            "osx-x64",
            "osx-x64",
            "YokiFrame.Workbench.Avalonia.app/Contents/MacOS/YokiFrame.Workbench.Avalonia",
            "yoki",
            "YokiFrame.Workbench.Avalonia.app"
        },
        {
            OSPlatform.OSX,
            Architecture.Arm64,
            "osx-arm64",
            "osx-arm64",
            "YokiFrame.Workbench.Avalonia.app/Contents/MacOS/YokiFrame.Workbench.Avalonia",
            "yoki",
            "YokiFrame.Workbench.Avalonia.app"
        }
    };

    /// <summary>
    /// 验证当前宿主系统和进程架构会解析为对应 profile 与两个共享入口。
    /// </summary>
    /// <param name="platform">宿主操作系统。</param>
    /// <param name="architecture">宿主进程架构。</param>
    /// <param name="runtimeIdentifier">期望 runtime identifier。</param>
    /// <param name="dotnetRuntimeIdentifier">期望传给 dotnet 的 RID。</param>
    /// <param name="guiEntry">期望 GUI 入口。</param>
    /// <param name="cliEntry">期望 CLI 入口。</param>
    /// <param name="macAppBundleName">期望 macOS app bundle 名；非 macOS 为空。</param>
    [Theory]
    [MemberData(nameof(SupportedProfiles))]
    public void ResolveMapsSupportedHostToRuntimeProfile(
        OSPlatform platform,
        Architecture architecture,
        string runtimeIdentifier,
        string dotnetRuntimeIdentifier,
        string guiEntry,
        string cliEntry,
        string macAppBundleName)
    {
        var profile = RuntimePublishProfileResolver.Resolve(platform, architecture);

        Assert.Equal(runtimeIdentifier, profile.RuntimeIdentifier);
        Assert.Equal(dotnetRuntimeIdentifier, profile.DotnetRuntimeIdentifier);
        Assert.Equal(guiEntry, profile.GuiEntry);
        Assert.Equal(cliEntry, profile.CliEntry);
        Assert.Equal(macAppBundleName, profile.MacAppBundleName);
    }

    /// <summary>
    /// 验证尚未发布的系统或架构会明确拒绝，而不是静默选择错误平台产物。
    /// </summary>
    /// <param name="platformName">宿主操作系统名。</param>
    /// <param name="architecture">宿主进程架构。</param>
    [Theory]
    [InlineData("WINDOWS", Architecture.Arm64)]
    [InlineData("LINUX", Architecture.Arm64)]
    [InlineData("FREEBSD", Architecture.X64)]
    public void ResolveRejectsUnsupportedHost(string platformName, Architecture architecture)
    {
        var platform = OSPlatform.Create(platformName);

        var exception = Assert.Throws<PlatformNotSupportedException>(
            () => RuntimePublishProfileResolver.Resolve(platform, architecture));

        Assert.Contains(platformName, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(architecture.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 Packaging 提供按受控 profile 标识解析发布配置的公开入口，供薄脚本和维护流程复用。
    /// </summary>
    [Fact]
    public void ResolveExposesAllowlistedProfileEntryPoint()
    {
        var method = typeof(RuntimePublishProfileResolver).GetMethod(
            nameof(RuntimePublishProfileResolver.Resolve),
            new[] { typeof(string), typeof(bool) });

        Assert.NotNull(method);
    }

    /// <summary>
    /// 验证 Windows Native AOT 使用独立 profile 目录、真实 win-x64 RID，并同时发布 GUI 和 CLI。
    /// </summary>
    [Fact]
    public void ResolveMapsNativeAotProfileToWindowsGuiAndCliRuntime()
    {
        var profile = RuntimePublishProfileResolver.Resolve("win-x64-aot", startupOptimized: false);

        Assert.Equal("win-x64-aot", profile.RuntimeIdentifier);
        Assert.Equal("win-x64", profile.DotnetRuntimeIdentifier);
        Assert.Equal("YokiFrame.Workbench.Avalonia.exe", profile.GuiEntry);
        Assert.Equal("yoki.exe", profile.CliEntry);
        Assert.False(GetBooleanProperty(profile, "SharedRuntime"));
    }

    /// <summary>
    /// 验证 managed、ReadyToRun 与 Native AOT profile 向发布服务暴露唯一且互斥的构建选项。
    /// </summary>
    /// <param name="runtimeIdentifier">目标 profile。</param>
    /// <param name="startupOptimized">是否请求 ReadyToRun。</param>
    /// <param name="publishCli">是否发布 CLI。</param>
    /// <param name="selfContained">当前 profile 是否自包含。</param>
    /// <param name="publishReadyToRun">是否启用 ReadyToRun。</param>
    /// <param name="publishAot">是否启用 Native AOT。</param>
    [Theory]
    [InlineData("win-x64", false, true, false, false, false)]
    [InlineData("win-x64", true, true, false, true, false)]
    [InlineData("win-x64-aot", false, true, true, false, true)]
    public void ResolveProvidesProfileBuildOptions(
        string runtimeIdentifier,
        bool startupOptimized,
        bool publishCli,
        bool selfContained,
        bool publishReadyToRun,
        bool publishAot)
    {
        var profile = RuntimePublishProfileResolver.Resolve(runtimeIdentifier, startupOptimized);

        Assert.Equal(publishCli, GetBooleanProperty(profile, "PublishCli"));
        Assert.Equal(selfContained, GetBooleanProperty(profile, "SelfContained"));
        Assert.Equal(publishReadyToRun, GetBooleanProperty(profile, "PublishReadyToRun"));
        Assert.Equal(publishAot, GetBooleanProperty(profile, "PublishAot"));
    }

    /// <summary>
    /// 读取 profile 的公开布尔构建选项；属性缺失时保留清晰的 TDD 失败原因。
    /// </summary>
    /// <param name="profile">待检查 profile。</param>
    /// <param name="propertyName">属性名。</param>
    /// <returns>布尔属性值。</returns>
    private static bool GetBooleanProperty(object profile, string propertyName)
    {
        var property = profile.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<bool>(property.GetValue(profile));
    }
}
