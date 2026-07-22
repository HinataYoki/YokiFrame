using System.Runtime.InteropServices;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 统一 Workbench Avalonia 测试的平台能力判定，避免在 Linux CI 上误跑 Windows 专用能力。
/// </summary>
internal static class WorkbenchTestPlatform
{
    /// <summary>
    /// 当前进程是否支持命名 Shared Memory map（Windows 命名段；Linux 无 CreateNew(name)）。
    /// </summary>
    internal static bool SupportsNamedMemoryMaps => OperatingSystem.IsWindows();

    /// <summary>
    /// 当前是否适合做 Installer 像素级/最低视口布局回归（Windows 桌面基线）。
    /// Linux headless 字体与布局度量与产品目标平台不一致，只保留逻辑控件断言。
    /// </summary>
    internal static bool SupportsInstallerPixelLayoutBaseline => OperatingSystem.IsWindows();

    /// <summary>
    /// 非 Windows 时返回可写进 xUnit Skip 的原因；Windows 返回 null。
    /// </summary>
    /// <param name="capability">能力短名，用于 Skip 文案。</param>
    /// <returns>Skip 原因或 null。</returns>
    internal static string? SkipUnlessWindows(string capability)
    {
        return OperatingSystem.IsWindows()
            ? null
            : capability + " requires Windows (current OS: " + RuntimeInformation.OSDescription + ").";
    }
}
