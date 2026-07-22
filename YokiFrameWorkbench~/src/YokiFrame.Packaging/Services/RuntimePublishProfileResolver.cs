using System.Runtime.InteropServices;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 将当前宿主系统与进程架构映射为受支持的项目级 Workbench Runtime profile。
/// </summary>
public static class RuntimePublishProfileResolver
{
    private const string GUI_NAME = "YokiFrame.Workbench.Avalonia";
    private const string CLI_NAME = "YokiFrame.Cli";
    private const string MAC_APP_BUNDLE_NAME = "YokiFrame.Workbench.Avalonia.app";
    private const string MAC_GUI_ENTRY = "YokiFrame.Workbench.Avalonia.app/Contents/MacOS/YokiFrame.Workbench.Avalonia";

    /// <summary>
    /// 解析当前进程所在平台的发布 profile。
    /// </summary>
    /// <returns>当前平台发布 profile。</returns>
    public static RuntimePublishProfile ResolveCurrent()
    {
        return Resolve(GetCurrentPlatform(), RuntimeInformation.ProcessArchitecture);
    }

    /// <summary>
    /// 根据宿主系统与进程架构解析发布 profile。
    /// </summary>
    /// <param name="platform">宿主操作系统。</param>
    /// <param name="architecture">宿主进程架构。</param>
    /// <returns>受支持的发布 profile。</returns>
    /// <exception cref="PlatformNotSupportedException">当前组合尚未进入首批发布矩阵时抛出。</exception>
    public static RuntimePublishProfile Resolve(OSPlatform platform, Architecture architecture)
    {
        if (platform.Equals(OSPlatform.Windows) && architecture == Architecture.X64)
        {
            return CreateWindowsAotProfile();
        }

        if (platform.Equals(OSPlatform.Linux) && architecture == Architecture.X64)
        {
            return CreateLinuxProfile();
        }

        if (platform.Equals(OSPlatform.OSX) && architecture is Architecture.X64 or Architecture.Arm64)
        {
            return CreateMacProfile(architecture);
        }

        throw new PlatformNotSupportedException(
            "WorkbenchRuntime publish is not supported for " + platform + " " + architecture + ".");
    }

    /// <summary>
    /// 按受控 profile 标识解析维护或 CI 发布配置，拒绝任意 RID 和路径片段。
    /// </summary>
    /// <param name="runtimeIdentifier">允许的 WorkbenchRuntime profile 标识。</param>
    /// <param name="startupOptimized">是否启用 ReadyToRun 启动优化。</param>
    /// <returns>受支持的发布 profile。</returns>
    /// <exception cref="PlatformNotSupportedException">profile 或优化组合不在发布矩阵时抛出。</exception>
    public static RuntimePublishProfile Resolve(string runtimeIdentifier, bool startupOptimized)
    {
        if (startupOptimized && !string.Equals(runtimeIdentifier, "win-x64", StringComparison.Ordinal))
        {
            throw new PlatformNotSupportedException(
                "Startup optimized WorkbenchRuntime publish is only supported for win-x64.");
        }

        return runtimeIdentifier switch
        {
            "win-x64" => CreateWindowsProfile(startupOptimized),
            "win-x64-aot" => CreateWindowsAotProfile(),
            "linux-x64" => CreateLinuxProfile(),
            "osx-x64" => CreateMacProfile(Architecture.X64),
            "osx-arm64" => CreateMacProfile(Architecture.Arm64),
            _ => throw new PlatformNotSupportedException(
                "WorkbenchRuntime profile is not supported: " + runtimeIdentifier + ".")
        };
    }

    /// <summary>
    /// 获取当前宿主操作系统标识。
    /// </summary>
    /// <returns>Windows、Linux 或 macOS 平台标识。</returns>
    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }

        return OSPlatform.Create(RuntimeInformation.OSDescription);
    }

    /// <summary>
    /// 创建 Windows x64 managed profile。
    /// </summary>
    /// <returns>Windows 发布 profile。</returns>
    private static RuntimePublishProfile CreateWindowsProfile(bool startupOptimized)
    {
        return new RuntimePublishProfile(
            "win-x64",
            "win-x64",
            GUI_NAME + ".exe",
            CLI_NAME + ".exe",
            GUI_NAME + ".exe",
            "yoki.exe",
            string.Empty,
            startupOptimized ? RuntimePublishMode.ReadyToRun : RuntimePublishMode.Managed);
    }

    /// <summary>
    /// 创建 Windows x64 Native AOT GUI 与 CLI profile；真实 dotnet RID 仍为 win-x64。
    /// </summary>
    /// <returns>Windows Native AOT 发布 profile。</returns>
    private static RuntimePublishProfile CreateWindowsAotProfile()
    {
        return new RuntimePublishProfile(
            "win-x64-aot",
            "win-x64",
            GUI_NAME + ".exe",
            CLI_NAME + ".exe",
            GUI_NAME + ".exe",
            "yoki.exe",
            string.Empty,
            RuntimePublishMode.NativeAot);
    }

    /// <summary>
    /// 创建 Linux x64 managed profile。
    /// </summary>
    /// <returns>Linux 发布 profile。</returns>
    private static RuntimePublishProfile CreateLinuxProfile()
    {
        return new RuntimePublishProfile(
            "linux-x64",
            "linux-x64",
            GUI_NAME,
            CLI_NAME,
            GUI_NAME,
            "yoki",
            string.Empty,
            RuntimePublishMode.Managed);
    }

    /// <summary>
    /// 创建 macOS managed profile。
    /// </summary>
    /// <param name="architecture">macOS 进程架构。</param>
    /// <returns>macOS 发布 profile。</returns>
    private static RuntimePublishProfile CreateMacProfile(Architecture architecture)
    {
        var runtimeIdentifier = architecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return new RuntimePublishProfile(
            runtimeIdentifier,
            runtimeIdentifier,
            GUI_NAME,
            CLI_NAME,
            MAC_GUI_ENTRY,
            "yoki",
            MAC_APP_BUNDLE_NAME,
            RuntimePublishMode.Managed);
    }
}
