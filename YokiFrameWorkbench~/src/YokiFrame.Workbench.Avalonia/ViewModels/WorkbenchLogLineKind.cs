namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述运行日志行的语义类型，用于在终端样式中区分命令发送、接收和错误。
/// </summary>
public enum WorkbenchLogLineKind
{
    /// <summary>
    /// 普通信息日志。
    /// </summary>
    Information,

    /// <summary>
    /// 发往引擎侧的命令日志。
    /// </summary>
    Outbound,

    /// <summary>
    /// 引擎侧返回的响应日志。
    /// </summary>
    Inbound,

    /// <summary>
    /// 失败、超时或临时错误日志。
    /// </summary>
    Error
}
