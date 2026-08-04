using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client;

/// <summary>
/// 提供引擎发现和 FileBridge 只读状态的窄读取端口。
/// </summary>
public interface IEngineStateReader
{
    /// <summary>获取当前项目的标准路径解析器。</summary>
    YokiFramePaths Paths { get; }

    /// <summary>读取当前项目注册的全部 engine。</summary>
    IReadOnlyList<EngineRegistryEntry> ReadEngineEntries();

    /// <summary>读取指定 engine 的 heartbeat。</summary>
    HeartbeatInfo? ReadHeartbeat(string engineId);

    /// <summary>读取指定 engine 的 FileBridge 队列状态。</summary>
    FileBridgeStatus ReadBridgeStatus(string engineId);
}
