namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Application 遇到没有 owner manifest 的 legacy 包时采用的策略。
/// </summary>
public enum InstallerLegacyPackagePolicy
{
    /// <summary>
    /// 拒绝接管并保持目标项目不变。
    /// </summary>
    Reject,

    /// <summary>
    /// 用户已明确确认接管，允许 Core 在备份后替换 legacy 包。
    /// </summary>
    TakeOverConfirmed
}
