namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Workbench 运行日志中的一行文本。
/// </summary>
public sealed class WorkbenchLogLine
{
    /// <summary>
    /// 创建日志行。
    /// </summary>
    /// <param name="timestamp">本地时间文本。</param>
    /// <param name="message">日志内容。</param>
    public WorkbenchLogLine(string timestamp, string message)
        : this(timestamp, message, WorkbenchLogLineKind.Information)
    {
    }

    /// <summary>
    /// 创建带语义类型的日志行。
    /// </summary>
    /// <param name="timestamp">本地时间文本。</param>
    /// <param name="message">日志内容。</param>
    /// <param name="kind">日志语义类型。</param>
    public WorkbenchLogLine(string timestamp, string message, WorkbenchLogLineKind kind)
    {
        Timestamp = timestamp;
        Message = message;
        Kind = kind;
    }

    /// <summary>
    /// 获取本地时间文本。
    /// </summary>
    public string Timestamp { get; }

    /// <summary>
    /// 获取日志内容。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取日志语义类型。
    /// </summary>
    public WorkbenchLogLineKind Kind { get; }

    /// <summary>
    /// 获取是否为发往引擎侧的命令日志。
    /// </summary>
    public bool IsOutbound => Kind == WorkbenchLogLineKind.Outbound;

    /// <summary>
    /// 获取是否为引擎侧返回的响应日志。
    /// </summary>
    public bool IsInbound => Kind == WorkbenchLogLineKind.Inbound;

    /// <summary>
    /// 获取是否为错误日志。
    /// </summary>
    public bool IsError => Kind == WorkbenchLogLineKind.Error;
}
