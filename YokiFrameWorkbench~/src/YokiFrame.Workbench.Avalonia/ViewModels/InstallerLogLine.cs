namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Installer 日志列表中的一行稳定显示文本。
/// </summary>
public sealed class InstallerLogLine
{
    /// <summary>
    /// 创建日志显示行。
    /// </summary>
    /// <param name="timestampText">本地时间文本。</param>
    /// <param name="message">日志消息。</param>
    public InstallerLogLine(string timestampText, string message)
    {
        TimestampText = timestampText;
        Message = message;
    }

    /// <summary>
    /// 获取带方括号的本地时间文本。
    /// </summary>
    public string TimestampText { get; }

    /// <summary>
    /// 获取日志消息。
    /// </summary>
    public string Message { get; }
}
