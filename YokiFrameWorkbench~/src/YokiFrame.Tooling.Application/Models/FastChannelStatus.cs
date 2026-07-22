using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述某个 engine 当前公开的 FastChannel endpoint 或可靠 FileBridge fallback 状态。
/// </summary>
public sealed class FastChannelStatus
{
    /// <summary>
    /// 创建 FastChannel 状态 read model，供 CLI、Workbench 和 AI 自动化使用同一 endpoint 选择结果。
    /// </summary>
    /// <param name="engineId">经过选择与安全校验的 engine 标识。</param>
    /// <param name="source">endpoint 来源，例如 engineRegistry 或 fallback。</param>
    /// <param name="endpoint">当前发布或合成的 endpoint。</param>
    public FastChannelStatus(string engineId, string source, FastChannelEndpoint endpoint)
    {
        EngineId = engineId;
        Source = source;
        Endpoint = endpoint;
    }

    /// <summary>
    /// 获取经过选择与安全校验的 engine 标识。
    /// </summary>
    public string EngineId { get; }

    /// <summary>
    /// 获取 endpoint 来源标识。
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// 获取当前发布或合成的 FastChannel endpoint。
    /// </summary>
    public FastChannelEndpoint Endpoint { get; }
}
