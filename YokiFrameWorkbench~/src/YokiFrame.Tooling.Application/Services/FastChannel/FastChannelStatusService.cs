using YokiFrame.Client;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 为 Workbench、CLI 和 AI 自动化读取当前 engine FastChannel endpoint，且不建立连接或发送命令。
/// </summary>
public sealed class FastChannelStatusService
{
    private const string ENGINE_REGISTRY_SOURCE = "engineRegistry";
    private const string FALLBACK_SOURCE = "fallback";
    private readonly IYokiFrameClient mClient;
    private readonly EngineSelectionService mEngineSelectionService;

    /// <summary>
    /// 使用统一 Client 创建 FastChannel 状态用例，避免入口层重复解析 registry 和 fallback。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public FastChannelStatusService(IYokiFrameClient client)
    {
        mClient = client;
        mEngineSelectionService = new EngineSelectionService(client);
    }

    /// <summary>
    /// 读取目标 engine 的 endpoint；registry 未声明 endpoint 时返回 disabled endpoint 与 FileBridge fallback。
    /// </summary>
    /// <param name="requestedEngineId">显式 engine 标识；为空时仅自动选择唯一在线 engine。</param>
    /// <returns>不会建立 FastChannel 连接的当前状态 read model。</returns>
    public FastChannelStatus GetStatus(string requestedEngineId)
    {
        var engineId = mEngineSelectionService.Resolve(requestedEngineId, DateTimeOffset.UtcNow);
        var registry = mClient.ReadEngineEntries().FirstOrDefault(
            entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
        if (registry == null)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "EngineNotRegistered",
                "Engine is not registered: " + engineId,
                "Start the target engine adapter or choose an engine returned by engine list.",
                new[] { mClient.Paths.EnginesRoot }));
        }

        var endpoint = ResolveEndpoint(registry, out var source);
        return new FastChannelStatus(engineId, source, endpoint);
    }

    /// <summary>
    /// 从 registry 选择第一个启用 endpoint；没有 endpoint 时生成 disabled endpoint，确保调用侧始终能识别 FileBridge fallback。
    /// </summary>
    /// <param name="registry">当前 engine registry。</param>
    /// <param name="source">输出 endpoint 来源标识。</param>
    /// <returns>发布 endpoint 或合成 disabled endpoint。</returns>
    private static FastChannelEndpoint ResolveEndpoint(EngineRegistryEntry registry, out string source)
    {
        var endpoint = registry.FastChannels.FirstOrDefault(static item => item.Enabled)
            ?? registry.FastChannels.FirstOrDefault();
        if (endpoint != null)
        {
            source = ENGINE_REGISTRY_SOURCE;
            return endpoint;
        }

        source = FALLBACK_SOURCE;
        return FastChannelEndpoint.Disabled(
            registry.EngineId,
            registry.SessionId,
            registry.Generation);
    }
}
