using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述共享命令用例最终选择的传输和 terminal response，供 Workbench 与 CLI 分别投影。
/// </summary>
public sealed class CommandExecutionResult
{
    /// <summary>
    /// 创建一次命令执行结果。
    /// </summary>
    /// <param name="transport">实际完成请求的传输标识。</param>
    /// <param name="commandPath">FileBridge command 证据路径；FastChannel 成功时为空。</param>
    /// <param name="responsePath">FileBridge response 证据路径；FastChannel 成功时为空。</param>
    /// <param name="response">宿主返回的 terminal response。</param>
    public CommandExecutionResult(
        string transport,
        string commandPath,
        string responsePath,
        CommandResponse response)
    {
        Transport = transport;
        CommandPath = commandPath;
        ResponsePath = responsePath;
        Response = response;
    }

    /// <summary>
    /// 获取实际完成请求的传输标识。
    /// </summary>
    public string Transport { get; }

    /// <summary>
    /// 获取 FileBridge command 证据路径；FastChannel 成功时为空。
    /// </summary>
    public string CommandPath { get; }

    /// <summary>
    /// 获取 FileBridge response 证据路径；FastChannel 成功时为空。
    /// </summary>
    public string ResponsePath { get; }

    /// <summary>
    /// 获取宿主返回的 terminal response。
    /// </summary>
    public CommandResponse Response { get; }
}
