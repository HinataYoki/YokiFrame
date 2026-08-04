using System.Text.Json.Nodes;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client;

/// <summary>
/// 定义 Workbench、Installer 和 CLI 访问 YokiFrame 宿主状态与命令的统一客户端边界。
/// </summary>
public interface IYokiFrameClient : IEngineStateReader, ICommandTransport, IFastChannelCommandTransport, ITelemetryReader
{
    /// <summary>
    /// 读取 harness capability 文件。
    /// </summary>
    /// <returns>capability JSON 节点。</returns>
    JsonNode ReadHarnessCapabilities();

    /// <summary>
    /// 读取指定 Kit 的 snapshot。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <returns>snapshot JSON 节点。</returns>
    JsonNode ReadSnapshot(string engineId, string kit, string name);

    // 状态、命令和 telemetry 成员由三个窄端口继承，避免组合接口维护第二套契约。
}
