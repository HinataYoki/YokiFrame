using YokiFrame.Installer.Core.IO;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 向 Installer UI 和启动路径解析暴露源目录与目标项目的重叠门控。
/// </summary>
public static class InstallerSourceTargetRules
{
    /// <summary>
    /// 获取拒绝把开发项目当作安装目标时的说明。
    /// </summary>
    public static string OverlapMessage => InstallerSourceTargetGuard.OverlapMessage;

    /// <summary>
    /// 判断源目录是否与目标项目相同或互相包含。
    /// </summary>
    /// <param name="sourcePackageRoot">本地 YokiFrame 源目录。</param>
    /// <param name="targetProjectRoot">目标项目根。</param>
    /// <returns>重叠时返回 true。</returns>
    public static bool Overlaps(string? sourcePackageRoot, string? targetProjectRoot)
    {
        return InstallerSourceTargetGuard.Overlaps(sourcePackageRoot, targetProjectRoot);
    }
}
