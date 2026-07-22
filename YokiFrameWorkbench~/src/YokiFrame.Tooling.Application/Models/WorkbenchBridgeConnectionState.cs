namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench 对当前 FileBridge 连接健康度的归类。
/// </summary>
public enum WorkbenchBridgeConnectionState
{
    /// <summary>
    /// 当前没有在线 engine，等待宿主启动或用户显式选择已知目录。
    /// </summary>
    EngineUnavailable,

    /// <summary>
    /// 当前有多个在线 engine，等待用户从候选中显式选择。
    /// </summary>
    EngineSelectionRequired,

    /// <summary>
    /// 已发现 engine registry 且 heartbeat 未超过 stale 阈值。
    /// </summary>
    Online,

    /// <summary>
    /// 已发现 heartbeat，但写入时间超过 stale 阈值。
    /// </summary>
    Stale,

    /// <summary>
    /// 已发现 engine registry，但 heartbeat 文件缺失。
    /// </summary>
    HeartbeatMissing,

    /// <summary>
    /// 未在 registry 中发现当前选中的 engine。
    /// </summary>
    EngineUnregistered,

    /// <summary>
    /// bridge 状态读取失败，无法完成健康度判断。
    /// </summary>
    Unavailable
}
