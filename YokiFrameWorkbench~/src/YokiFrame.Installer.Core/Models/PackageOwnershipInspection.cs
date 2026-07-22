namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述目标包所有权状态及需要用户处理的相对冲突路径。
/// </summary>
public sealed class PackageOwnershipInspection
{
    /// <summary>
    /// 创建所有权检查结果。
    /// </summary>
    /// <param name="state">目标包所有权状态。</param>
    /// <param name="conflictPaths">稳定排序的包相对冲突路径。</param>
    public PackageOwnershipInspection(PackageOwnershipState state, IReadOnlyList<string> conflictPaths)
    {
        State = state;
        ConflictPaths = conflictPaths;
    }

    /// <summary>
    /// 获取目标包所有权状态。
    /// </summary>
    public PackageOwnershipState State { get; }

    /// <summary>
    /// 获取稳定排序的包相对冲突路径。
    /// </summary>
    public IReadOnlyList<string> ConflictPaths { get; }
}
