namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Installer 应用日志严重度。
/// </summary>
public enum InstallerLogLevel
{
    /// <summary>
    /// 普通状态和进度信息。
    /// </summary>
    Information,

    /// <summary>
    /// 需要用户确认或处理的可恢复问题。
    /// </summary>
    Warning,

    /// <summary>
    /// 阻止当前会话继续的失败。
    /// </summary>
    Error
}

/// <summary>
/// 描述带稳定 UTC 时间戳的 Installer 应用日志。
/// </summary>
public sealed class InstallerLogEntry
{
    /// <summary>
    /// 创建 Installer 日志项。
    /// </summary>
    /// <param name="timestampUtc">日志 UTC 时间。</param>
    /// <param name="level">日志严重度。</param>
    /// <param name="message">非空日志说明。</param>
    public InstallerLogEntry(DateTimeOffset timestampUtc, InstallerLogLevel level, string message)
    {
        TimestampUtc = timestampUtc;
        Level = level;
        Message = message;
    }

    /// <summary>
    /// 获取日志 UTC 时间。
    /// </summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>
    /// 获取日志严重度。
    /// </summary>
    public InstallerLogLevel Level { get; }

    /// <summary>
    /// 获取日志说明。
    /// </summary>
    public string Message { get; }
}
