namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 表示安装因 legacy 接管未确认或受管内容修改而在安全边界被拒绝。
/// </summary>
public sealed class InstallerConflictException : InvalidOperationException
{
    /// <summary>
    /// 创建 Application 自有安装冲突。
    /// </summary>
    /// <param name="message">冲突说明。</param>
    /// <param name="conflictPaths">稳定相对冲突路径。</param>
    public InstallerConflictException(string message, IReadOnlyList<string> conflictPaths)
        : base(message)
    {
        ConflictPaths = conflictPaths.ToArray();
    }

    /// <summary>
    /// 获取稳定相对冲突路径。
    /// </summary>
    public IReadOnlyList<string> ConflictPaths { get; }
}

/// <summary>
/// 表示 Core 写事务失败，并携带统一回滚结果和诊断证据。
/// </summary>
public sealed class InstallerExecutionException : IOException
{
    /// <summary>
    /// 创建 Application 自有执行异常。
    /// </summary>
    /// <param name="message">失败说明。</param>
    /// <param name="rollbackSucceeded">是否恢复事务前状态。</param>
    /// <param name="evidencePaths">诊断证据路径。</param>
    /// <param name="innerException">原始 Core 异常。</param>
    public InstallerExecutionException(
        string message,
        bool rollbackSucceeded,
        IReadOnlyList<string> evidencePaths,
        Exception innerException)
        : base(message, innerException)
    {
        RollbackSucceeded = rollbackSucceeded;
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>
    /// 获取是否恢复事务前状态。
    /// </summary>
    public bool RollbackSucceeded { get; }

    /// <summary>
    /// 获取诊断证据路径快照。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
