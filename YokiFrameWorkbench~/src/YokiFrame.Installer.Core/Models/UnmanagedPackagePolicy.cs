namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 指定 Installer 遇到无 owner manifest 的 legacy 包时采用的策略。
/// </summary>
public enum UnmanagedPackagePolicy
{
    /// <summary>
    /// 拒绝接管并保持目标项目逐字节不变。
    /// </summary>
    Reject,

    /// <summary>
    /// 用户已经明确确认接管，允许事务备份后替换 legacy 包。
    /// </summary>
    TakeOverConfirmed
}
