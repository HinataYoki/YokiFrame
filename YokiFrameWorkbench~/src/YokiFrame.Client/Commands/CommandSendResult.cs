using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client.Commands;

/// <summary>
/// 表示一次 command send 写入和等待响应后的结果。
/// </summary>
public sealed class CommandSendResult
{
    /// <summary>
    /// 创建命令发送结果。
    /// </summary>
    /// <param name="envelope">实际写入的命令信封。</param>
    /// <param name="commandPath">命令文件路径。</param>
    /// <param name="responsePath">预期响应路径。</param>
    /// <param name="response">读取到的响应。</param>
    public CommandSendResult(
        CommandEnvelope envelope,
        string commandPath,
        string responsePath,
        CommandResponse response)
    {
        Envelope = envelope;
        CommandPath = commandPath;
        ResponsePath = responsePath;
        Response = response;
    }

    /// <summary>
    /// 获取命令信封。
    /// </summary>
    public CommandEnvelope Envelope { get; }

    /// <summary>
    /// 获取命令文件路径。
    /// </summary>
    public string CommandPath { get; }

    /// <summary>
    /// 获取响应文件路径。
    /// </summary>
    public string ResponsePath { get; }

    /// <summary>
    /// 获取命令响应。
    /// </summary>
    public CommandResponse Response { get; }
}
