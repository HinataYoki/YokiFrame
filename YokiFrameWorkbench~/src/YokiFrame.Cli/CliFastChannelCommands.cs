using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Cli;

/// <summary>
/// 提供 FastChannel 相关 CLI 命令；当前只做 endpoint 观测，不主动建立连接。
/// </summary>
internal static class CliFastChannelCommands
{
    /// <summary>
    /// 输出当前 engine registry 中声明的 FastChannel endpoint；缺失时返回 disabled endpoint 和 FileBridge fallback。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    public static int WriteStatus(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var requestedEngineId = commandLine.GetOption("engine", string.Empty);
        var status = new FastChannelStatusService(client).GetStatus(requestedEngineId);
        JsonObject payload = new()
        {
            ["command"] = "fastchannel status",
            ["engineId"] = status.EngineId,
            ["source"] = status.Source,
            ["endpoint"] = CliJsonOutput.ToJsonNode(status.Endpoint),
            ["fallback"] = status.Endpoint.Fallback
        };
        return CliJsonOutput.WriteSuccess(payload);
    }
}
