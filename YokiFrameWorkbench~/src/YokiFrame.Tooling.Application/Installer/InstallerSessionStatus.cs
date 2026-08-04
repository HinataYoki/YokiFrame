namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Installer 应用会话对 UI 与 CLI 可见的稳定状态。
/// </summary>
public enum InstallerSessionStatus
{
    /// <summary>
    /// 尚未开始检测。
    /// </summary>
    Idle,

    /// <summary>
    /// 正在检测输入并生成安装计划。
    /// </summary>
    Detecting,

    /// <summary>
    /// 安装计划已生成，可提交执行。
    /// </summary>
    PlanReady,

    /// <summary>
    /// 正在 staging、备份或提交安装内容。
    /// </summary>
    Applying,

    /// <summary>
    /// 正在校验 staging 或正式安装结果。
    /// </summary>
    Verifying,

    /// <summary>
    /// 安装失败后正在恢复事务前状态。
    /// </summary>
    RollingBack,

    /// <summary>
    /// 安装和校验均已成功。
    /// </summary>
    Succeeded,

    /// <summary>
    /// Core 投影已经提交，但宿主构建或 owner post-verify 尚未成功完成。
    /// </summary>
    CommittedNeedsVerification,

    /// <summary>
    /// 检测到 legacy 未确认接管或受管文件修改冲突。
    /// </summary>
    Conflict,

    /// <summary>
    /// 检测、规划或安装事务失败。
    /// </summary>
    Failed,

    /// <summary>
    /// 调用方取消了当前检测或安装；该状态不表示宿主没有执行任何写入。
    /// </summary>
    Cancelled
}
