namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示目标包相对于 Installer owner manifest 的所有权状态。
/// </summary>
public enum PackageOwnershipState
{
    /// <summary>
    /// 目标包目录尚不存在。
    /// </summary>
    NotInstalled,

    /// <summary>
    /// 目标包存在但没有新版 owner manifest。
    /// </summary>
    UnmanagedLegacy,

    /// <summary>
    /// 目标包文件与 manifest 完全匹配。
    /// </summary>
    Clean,

    /// <summary>
    /// 受管文件缺失、内容变化或出现额外文件。
    /// </summary>
    Modified
}
