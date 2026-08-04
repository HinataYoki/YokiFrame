namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示上一次 Installer 事务的持久状态不足以安全自动恢复。
/// </summary>
public sealed class InstallerRecoveryRequiredException : IOException
{
    /// <summary>
    /// 创建恢复阻断异常。
    /// </summary>
    /// <param name="projectRoot">受影响的项目根。</param>
    /// <param name="journalPath">需要人工诊断的 journal 路径。</param>
    /// <param name="message">恢复原因。</param>
    public InstallerRecoveryRequiredException(
        string projectRoot,
        string journalPath,
        string message)
        : base(message)
    {
        ProjectRoot = projectRoot;
        JournalPath = journalPath;
    }

    /// <summary>
    /// 获取受影响的项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取持久 journal 路径。
    /// </summary>
    public string JournalPath { get; }
}
