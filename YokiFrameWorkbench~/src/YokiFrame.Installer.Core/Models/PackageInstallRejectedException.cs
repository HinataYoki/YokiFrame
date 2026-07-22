namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示目标包存在用户修改或 legacy 接管尚未确认，因此事务在零写入点被拒绝。
/// </summary>
public sealed class PackageInstallRejectedException : InvalidOperationException
{
    /// <summary>
    /// 创建安装拒绝异常。
    /// </summary>
    /// <param name="ownershipState">触发拒绝的所有权状态。</param>
    /// <param name="conflictPaths">受管修改对应的相对冲突路径。</param>
    public PackageInstallRejectedException(
        PackageOwnershipState ownershipState,
        IReadOnlyList<string> conflictPaths)
        : base(CreateMessage(ownershipState, conflictPaths))
    {
        OwnershipState = ownershipState;
        ConflictPaths = conflictPaths;
    }

    /// <summary>
    /// 获取触发拒绝的所有权状态。
    /// </summary>
    public PackageOwnershipState OwnershipState { get; }

    /// <summary>
    /// 获取稳定排序的相对冲突路径。
    /// </summary>
    public IReadOnlyList<string> ConflictPaths { get; }

    /// <summary>
    /// 根据所有权状态创建面向用户的拒绝说明。
    /// </summary>
    /// <param name="state">所有权状态。</param>
    /// <param name="conflictPaths">冲突路径。</param>
    /// <returns>拒绝说明。</returns>
    private static string CreateMessage(PackageOwnershipState state, IReadOnlyList<string> conflictPaths)
    {
        return state == PackageOwnershipState.UnmanagedLegacy
            ? "Existing YokiFrame package is unmanaged legacy content and requires explicit takeover confirmation."
            : "Existing YokiFrame package contains managed modifications: " + string.Join(", ", conflictPaths);
    }
}
