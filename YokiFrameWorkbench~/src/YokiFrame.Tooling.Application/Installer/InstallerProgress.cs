namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Core gateway 可上报的执行阶段。
/// </summary>
public enum InstallerProgressStage
{
    /// <summary>
    /// 正在应用安装计划。
    /// </summary>
    Applying,

    /// <summary>
    /// 正在校验安装结果。
    /// </summary>
    Verifying,

    /// <summary>
    /// 正在执行失败回滚。
    /// </summary>
    RollingBack
}

/// <summary>
/// 描述一次可显示的 Installer 执行进度更新。
/// </summary>
public sealed class InstallerProgressUpdate
{
    /// <summary>
    /// 创建执行进度更新。
    /// </summary>
    /// <param name="stage">执行阶段。</param>
    /// <param name="completed">已完成工作单元。</param>
    /// <param name="total">总工作单元。</param>
    /// <param name="message">当前阶段说明。</param>
    public InstallerProgressUpdate(InstallerProgressStage stage, int completed, int total, string message)
    {
        if (completed < 0 || total <= 0 || completed > total)
        {
            throw new ArgumentOutOfRangeException(nameof(completed), "Installer progress must satisfy 0 <= completed <= total.");
        }

        Stage = stage;
        Completed = completed;
        Total = total;
        Message = string.IsNullOrWhiteSpace(message) ? stage.ToString() : message;
    }

    /// <summary>
    /// 获取执行阶段。
    /// </summary>
    public InstallerProgressStage Stage { get; }

    /// <summary>
    /// 获取已完成工作单元。
    /// </summary>
    public int Completed { get; }

    /// <summary>
    /// 获取总工作单元。
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// 获取当前阶段说明。
    /// </summary>
    public string Message { get; }
}
