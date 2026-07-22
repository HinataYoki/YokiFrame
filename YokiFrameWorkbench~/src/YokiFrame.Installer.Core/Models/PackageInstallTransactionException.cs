namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示 staging、备份、提交或复验失败，并公开持久化诊断与回滚结果。
/// </summary>
public sealed class PackageInstallTransactionException : IOException
{
    /// <summary>
    /// 创建事务失败异常。
    /// </summary>
    /// <param name="message">失败说明。</param>
    /// <param name="diagnosticEvidencePath">持久化诊断 JSON 路径。</param>
    /// <param name="rollbackSucceeded">是否已恢复事务前正式包状态。</param>
    /// <param name="innerException">原始失败。</param>
    public PackageInstallTransactionException(
        string message,
        string diagnosticEvidencePath,
        bool rollbackSucceeded,
        Exception innerException)
        : base(message, innerException)
    {
        DiagnosticEvidencePath = diagnosticEvidencePath;
        RollbackSucceeded = rollbackSucceeded;
    }

    /// <summary>
    /// 获取持久化诊断 JSON 路径。
    /// </summary>
    public string DiagnosticEvidencePath { get; }

    /// <summary>
    /// 获取是否已恢复事务前正式包状态。
    /// </summary>
    public bool RollbackSucceeded { get; }
}
