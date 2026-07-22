namespace YokiFrame.Tooling.Application.Engines;

/// <summary>
/// 描述工具应用层对当前 engine 选择的判定结果。
/// </summary>
public enum EngineSelectionStatus
{
    /// <summary>
    /// 已得到可安全使用的目标 engine。
    /// </summary>
    Selected,

    /// <summary>
    /// 当前没有 heartbeat 在线的 engine。
    /// </summary>
    Unavailable,

    /// <summary>
    /// 当前有多个在线 engine，需要调用方显式选择。
    /// </summary>
    SelectionRequired
}
