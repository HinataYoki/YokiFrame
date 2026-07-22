namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Installer 会话任一时刻可供 UI 或 CLI 消费的不可变快照。
/// </summary>
public sealed record InstallerSessionState
{
    /// <summary>
    /// 获取当前会话状态。
    /// </summary>
    public InstallerSessionStatus Status { get; internal init; } = InstallerSessionStatus.Idle;

    /// <summary>
    /// 获取当前会话输入；Idle 时为空。
    /// </summary>
    public InstallerInstallOptions? Options { get; internal init; }

    /// <summary>
    /// 获取已生成的 Core 安装计划。
    /// </summary>
    public InstallerPlanPreview? Plan { get; internal init; }

    /// <summary>
    /// 获取成功提交后的 Core 事务结果。
    /// </summary>
    public InstallerExecutionResult? Result { get; internal init; }

    /// <summary>
    /// 获取最近一次执行进度。
    /// </summary>
    public InstallerProgressUpdate? Progress { get; internal init; }

    /// <summary>
    /// 获取当前会话按产生顺序排列的日志快照。
    /// </summary>
    public IReadOnlyList<InstallerLogEntry> Logs { get; internal init; } = Array.Empty<InstallerLogEntry>();

    /// <summary>
    /// 获取导致 Conflict 的相对路径。
    /// </summary>
    public IReadOnlyList<string> ConflictPaths { get; internal init; } = Array.Empty<string>();

    /// <summary>
    /// 获取失败诊断证据路径。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; internal init; } = Array.Empty<string>();

    /// <summary>
    /// 获取事务失败后的回滚结果；非事务失败时为空。
    /// </summary>
    public bool? RollbackSucceeded { get; internal init; }

    /// <summary>
    /// 获取 Conflict 或 Failed 的错误说明。
    /// </summary>
    public string ErrorMessage { get; internal init; } = string.Empty;

    /// <summary>
    /// 获取当前失败是否可通过从源码包构建项目 Runtime 缓存后恢复。
    /// </summary>
    public bool RuntimeBootstrapRequired { get; internal init; }
}

/// <summary>
/// 携带 Installer 会话状态变化后的不可变快照。
/// </summary>
public sealed class InstallerSessionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 创建状态变化事件参数。
    /// </summary>
    /// <param name="state">变化后的状态快照。</param>
    public InstallerSessionStateChangedEventArgs(InstallerSessionState state)
    {
        State = state;
    }

    /// <summary>
    /// 获取变化后的状态快照。
    /// </summary>
    public InstallerSessionState State { get; }
}
