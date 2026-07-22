using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 负责 Workbench Shell 的 engine selector 与每个会话一次的命令目录初始化。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    private string mCommandCatalogEngineId = string.Empty;

    /// <summary>
    /// 设置当前 engine，并在用户主动切换时回调窗口刷新 dashboard。
    /// </summary>
    /// <param name="engineId">新选中的 engine 标识。</param>
    private void SetSelectedEngine(string engineId)
    {
        var normalizedEngineId = engineId ?? string.Empty;
        if (!SetProperty(ref mSelectedEngineId, normalizedEngineId))
        {
            return;
        }

        if (!mIsUpdatingEngines && !string.IsNullOrWhiteSpace(normalizedEngineId))
        {
            mEngineChanged(normalizedEngineId);
        }
    }

    /// <summary>
    /// 根据 dashboard 中的真实 registry 刷新 selector，并排除空标识和重复项。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    private void UpdateEngineSelector(WorkbenchDashboardState state)
    {
        mIsUpdatingEngines = true;
        var engineIds = state.Engines
            .Select(static engine => engine.EngineId)
            .Where(static engineId => !string.IsNullOrWhiteSpace(engineId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!string.IsNullOrWhiteSpace(state.SelectedEngineId)
            && !engineIds.Contains(state.SelectedEngineId, StringComparer.Ordinal))
        {
            engineIds.Insert(0, state.SelectedEngineId);
        }

        EngineIds = engineIds;
        SelectedEngineId = state.SelectedEngineId;
        mIsUpdatingEngines = false;
    }

    /// <summary>
    /// 在 dashboard 确认有效选择后为每个新 engine 自动读取一次命令目录。
    /// </summary>
    /// <param name="state">已应用到 Shell 的 dashboard 状态。</param>
    private void RefreshCommandCatalogForSelectedEngine(WorkbenchDashboardState state)
    {
        var engineId = state.SelectedEngineId;
        if (string.IsNullOrWhiteSpace(engineId))
        {
            mCommandCatalogEngineId = string.Empty;
            return;
        }

        if (string.Equals(mCommandCatalogEngineId, engineId, StringComparison.Ordinal))
        {
            return;
        }

        mCommandCatalogEngineId = engineId;
        _ = mCommandRequested("System", "list_commands");
    }
}
