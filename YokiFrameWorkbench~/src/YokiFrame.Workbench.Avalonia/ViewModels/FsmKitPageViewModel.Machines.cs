using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>维护 FsmKit 左侧列表的稳定对象身份、筛选和顺序。</summary>
public sealed partial class FsmKitPageViewModel
{
    private readonly Dictionary<string, FsmMachineListItemViewModel> mMachineItemsById =
        new(StringComparer.Ordinal);
    private readonly ObservableCollection<FsmMachineListItemViewModel> mMachines = new();
    private IReadOnlyList<FsmMachineListItemViewModel> mAllMachines =
        Array.Empty<FsmMachineListItemViewModel>();

    /// <summary>获取保持对象身份稳定的筛选结果；集合实例在页面生命周期内不替换。</summary>
    public IReadOnlyList<FsmMachineListItemViewModel> Machines => mMachines;

    /// <summary>
    /// 按 instanceId 合并最新摘要，并仅对真实新增、删除或排序变化修改可见集合。
    /// </summary>
    /// <param name="summaries">Runtime 当前按注册顺序返回的摘要。</param>
    private void ApplyMachineSummaries(IReadOnlyList<WorkbenchFsmMachineSummary> summaries)
    {
        ApplyMachineSummaries(summaries, null);
    }

    /// <summary>合并低频 overview 摘要，同时保留当前实例由精确详情帧确认的可见字段。</summary>
    /// <param name="summaries">Runtime 当前按注册顺序返回的摘要。</param>
    private void ApplyMachineSummariesPreservingSelected(
        IReadOnlyList<WorkbenchFsmMachineSummary> summaries)
    {
        var preservedItem = mSelectedDetailsState?.Selected?.InstanceId == SelectedInstanceId
            ? SelectedMachine
            : null;
        ApplyMachineSummaries(summaries, preservedItem);
    }

    /// <summary>执行摘要合并，并可跳过当前精确详情拥有的稳定列表项。</summary>
    /// <param name="summaries">Runtime 当前按注册顺序返回的摘要。</param>
    /// <param name="preservedItem">需要保留字段的精确详情列表项；没有时为空。</param>
    private void ApplyMachineSummaries(
        IReadOnlyList<WorkbenchFsmMachineSummary> summaries,
        FsmMachineListItemViewModel? preservedItem)
    {
        HashSet<string> activeIds = new(StringComparer.Ordinal);
        List<FsmMachineListItemViewModel> nextItems = new(summaries.Count);
        for (var index = 0; index < summaries.Count; index++)
        {
            var summary = summaries[index];
            activeIds.Add(summary.InstanceId);
            if (!mMachineItemsById.TryGetValue(summary.InstanceId, out var item))
            {
                item = new FsmMachineListItemViewModel(summary);
                mMachineItemsById.Add(summary.InstanceId, item);
            }
            else if (!ReferenceEquals(item, preservedItem))
            {
                item.Apply(summary);
            }

            nextItems.Add(item);
        }

        RemoveInactiveMachineItems(activeIds);
        mAllMachines = nextItems;
        RebuildMachineList();
    }

    /// <summary>移除 Runtime 已注销的实例缓存，避免 session 内无界保留列表项。</summary>
    /// <param name="activeIds">本轮仍存在的 instanceId。</param>
    private void RemoveInactiveMachineItems(ISet<string> activeIds)
    {
        List<string> staleIds = new();
        foreach (var instanceId in mMachineItemsById.Keys)
        {
            if (!activeIds.Contains(instanceId))
            {
                staleIds.Add(instanceId);
            }
        }

        for (var index = 0; index < staleIds.Count; index++)
        {
            mMachineItemsById.Remove(staleIds[index]);
        }
    }

    /// <summary>根据当前搜索词增量同步可见项，不整体替换 ItemsSource。</summary>
    private void RebuildMachineList()
    {
        string query = SearchText.Trim();
        List<FsmMachineListItemViewModel> desiredItems = new(mAllMachines.Count);
        for (var index = 0; index < mAllMachines.Count; index++)
        {
            var item = mAllMachines[index];
            if (item.Matches(query)
                || string.Equals(item.InstanceId, SelectedInstanceId, StringComparison.Ordinal))
            {
                desiredItems.Add(item);
            }
        }

        SynchronizeVisibleMachines(desiredItems);
    }

    /// <summary>以最少 Insert/Move/Remove 操作同步 ObservableCollection，保留现有容器和选择。</summary>
    /// <param name="desiredItems">筛选后按 Runtime 顺序排列的稳定项。</param>
    private void SynchronizeVisibleMachines(IReadOnlyList<FsmMachineListItemViewModel> desiredItems)
    {
        for (var targetIndex = 0; targetIndex < desiredItems.Count; targetIndex++)
        {
            var desired = desiredItems[targetIndex];
            if (targetIndex < mMachines.Count && ReferenceEquals(mMachines[targetIndex], desired))
            {
                continue;
            }

            int currentIndex = mMachines.IndexOf(desired);
            if (currentIndex >= 0)
            {
                mMachines.Move(currentIndex, targetIndex);
            }
            else
            {
                mMachines.Insert(targetIndex, desired);
            }
        }

        while (mMachines.Count > desiredItems.Count)
        {
            mMachines.RemoveAt(mMachines.Count - 1);
        }
    }

    /// <summary>按稳定 instanceId 查找当前列表项。</summary>
    /// <param name="instanceId">目标实例标识。</param>
    /// <returns>当前 session 中的稳定列表项；不存在时为空。</returns>
    private FsmMachineListItemViewModel? FindMachine(string instanceId)
    {
        return string.IsNullOrWhiteSpace(instanceId)
            ? null
            : mMachineItemsById.GetValueOrDefault(instanceId);
    }

    /// <summary>按用户选择、payload 选择和 Runtime 顺序确定当前应展示的稳定列表项。</summary>
    /// <param name="state">本轮 Application 强类型状态。</param>
    /// <returns>当前应展示的实例；宿主没有活动实例时返回空。</returns>
    private FsmMachineListItemViewModel? FindPreferredMachine(WorkbenchFsmKitState state)
    {
        var preferredId = !string.IsNullOrWhiteSpace(SelectedInstanceId)
            ? SelectedInstanceId
            : state.Selected?.InstanceId ?? state.InstanceId;
        return FindMachine(preferredId)
            ?? (mAllMachines.Count > 0 ? mAllMachines[0] : null);
    }

    /// <summary>清空全部列表缓存和可见项，用于宿主 session 切换。</summary>
    private void ClearMachineList()
    {
        mMachineItemsById.Clear();
        mAllMachines = Array.Empty<FsmMachineListItemViewModel>();
        mMachines.Clear();
    }
}
