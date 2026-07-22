using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Tooling.Application.Engines;

/// <summary>
/// 描述 engine 选择结果，并为可交互工具保留恢复所需的候选和标准错误。
/// </summary>
public sealed class EngineSelectionResult
{
    /// <summary>
    /// 创建不可变 engine 选择结果；仅由选择服务和同程序集 read model 组装。
    /// </summary>
    /// <param name="status">选择状态。</param>
    /// <param name="selectedEngineId">已选择的 engine；未选择时为空。</param>
    /// <param name="onlineEngineIds">当前在线 engine，按标识稳定排序。</param>
    /// <param name="error">未选择时的标准错误；选择成功时为空。</param>
    internal EngineSelectionResult(
        EngineSelectionStatus status,
        string selectedEngineId,
        IReadOnlyList<string> onlineEngineIds,
        YokiFrameError? error)
    {
        Status = status;
        SelectedEngineId = selectedEngineId;
        OnlineEngineIds = onlineEngineIds;
        Error = error;
    }

    /// <summary>
    /// 获取选择状态。
    /// </summary>
    public EngineSelectionStatus Status { get; }

    /// <summary>
    /// 获取已选择的 engine；未选择时为空字符串。
    /// </summary>
    public string SelectedEngineId { get; }

    /// <summary>
    /// 获取当前在线 engine 候选，顺序稳定且不包含空值。
    /// </summary>
    public IReadOnlyList<string> OnlineEngineIds { get; }

    /// <summary>
    /// 获取未选择时的标准错误；选择成功时为 null。
    /// </summary>
    public YokiFrameError? Error { get; }

    /// <summary>
    /// 获取当前结果是否已选择有效 engine。
    /// </summary>
    public bool IsSelected => Status == EngineSelectionStatus.Selected;

    /// <summary>
    /// 创建选择成功结果，不额外触发 registry 或 heartbeat 读取。
    /// </summary>
    /// <param name="engineId">已通过安全校验的 engine 标识。</param>
    /// <param name="onlineEngineIds">自动发现时确认在线的 engine；显式选择时为空。</param>
    /// <returns>选择成功结果。</returns>
    internal static EngineSelectionResult CreateSelected(
        string engineId,
        IReadOnlyList<string>? onlineEngineIds = null)
    {
        var safeEngineId = SafeIdValidator.EnsureSafeId(engineId, nameof(engineId));
        return new EngineSelectionResult(
            EngineSelectionStatus.Selected,
            safeEngineId,
            onlineEngineIds?.ToArray() ?? Array.Empty<string>(),
            null);
    }

    /// <summary>
    /// 创建等待恢复的选择结果，并复制候选列表避免调用方后续修改。
    /// </summary>
    /// <param name="status">Unavailable 或 SelectionRequired。</param>
    /// <param name="onlineEngineIds">当前在线 engine 候选。</param>
    /// <param name="error">对应的标准错误。</param>
    /// <returns>未选择结果。</returns>
    internal static EngineSelectionResult CreatePending(
        EngineSelectionStatus status,
        IReadOnlyList<string> onlineEngineIds,
        YokiFrameError error)
    {
        return new EngineSelectionResult(
            status,
            string.Empty,
            onlineEngineIds.ToArray(),
            error);
    }
}
