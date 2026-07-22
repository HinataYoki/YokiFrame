namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 定义 Tooling.Application 调用 Installer.Core 的最小编排边界。
/// </summary>
public interface IInstallerWorkflowGateway
{
    /// <summary>
    /// 检测输入并生成不写入目标项目的安装计划。
    /// </summary>
    /// <param name="options">安装输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Application 自有安装预览。</returns>
    Task<InstallerPlanPreview> CreatePlanAsync(
        InstallerInstallOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// 执行已确认的安装计划，并把应用、校验和回滚进度回传应用层。
    /// </summary>
    /// <param name="options">生成计划时使用的安装输入。</param>
    /// <param name="plan">待执行的 Application 安装预览。</param>
    /// <param name="progress">同步接收执行进度的通道。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功提交后的 Application 执行结果。</returns>
    Task<InstallerExecutionResult> ExecuteAsync(
        InstallerInstallOptions options,
        InstallerPlanPreview plan,
        IProgress<InstallerProgressUpdate> progress,
        CancellationToken cancellationToken);
}
