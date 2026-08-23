using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 PoolKit 稳定集合、搜索和选择协调。</summary>
public sealed partial class PoolKitPageViewModel
{
    private readonly Dictionary<string, PoolKitPoolListItemViewModel> mPoolsByIdentity = new(StringComparer.Ordinal);
    private readonly List<PoolKitPoolListItemViewModel> mAllPools = new();
    private readonly List<PoolKitPoolListItemViewModel> mDesiredPools = new();
    private readonly Dictionary<string, PoolKitEventListItemViewModel> mEventsByIdentity = new(StringComparer.Ordinal);
    private readonly List<PoolKitEventListItemViewModel> mDesiredEvents = new();
    private readonly List<WorkbenchPoolKitEvent> mAllEvents = new();
    private readonly List<string> mStaleKeys = new();
    private string mSearchText = string.Empty;
    private PoolKitPoolListItemViewModel? mSelectedPool;

    /// <summary>获取或设置对象池搜索文本。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value ?? string.Empty)) ReconcileVisiblePools();
        }
    }

    /// <summary>获取或设置当前对象池选择。</summary>
    public PoolKitPoolListItemViewModel? SelectedPool
    {
        get => mSelectedPool;
        set
        {
            if (!SetProperty(ref mSelectedPool, value)) return;
            ReconcileSelectedEvents();
            NotifySelectionProperties();
        }
    }

    /// <summary>获取是否存在当前选择。</summary>
    public bool HasSelection => SelectedPool != null;
    /// <summary>获取是否等待选择。</summary>
    public bool IsSelectionEmpty => !HasSelection;
    /// <summary>获取 Runtime 已连接但尚未创建任何对象池。</summary>
    public bool HasNoRuntimePools => !IsWaitingForData && PoolTotal == 0;
    /// <summary>获取对象池存在但当前搜索没有结果。</summary>
    public bool HasNoSearchResults => !IsWaitingForData && PoolTotal > 0 && Pools.Count == 0;
    /// <summary>获取带当前关键字的搜索空状态文案。</summary>
    public string SearchEmptyText => string.Format(
        GetString("String.PoolKit.SearchNoMatchTemplate", "未找到匹配 “{0}” 的对象池"), SearchText);
    /// <summary>获取当前对象池名称。</summary>
    public string SelectedName => SelectedPool?.Name
        ?? GetString("String.PoolKit.NoSelection", "未选择对象池");
    /// <summary>获取当前对象池类型。</summary>
    public string SelectedTypeName => SelectedPool?.TypeName ?? "--";
    /// <summary>获取当前借出数量。</summary>
    public int SelectedActiveCount => SelectedPool?.ActiveCount ?? 0;
    /// <summary>获取当前池内数量。</summary>
    public int SelectedInactiveCount => SelectedPool?.InactiveCount ?? 0;
    /// <summary>获取当前总量。</summary>
    public int SelectedTotalCount => SelectedPool?.TotalCount ?? 0;
    /// <summary>获取当前峰值。</summary>
    public int SelectedPeakCount => SelectedPool?.PeakCount ?? 0;
    /// <summary>获取当前缓存上限。</summary>
    public string SelectedMaxCacheCountText => SelectedPool?.MaxCacheCountText ?? "--";
    /// <summary>获取当前使用率。</summary>
    public double SelectedUsagePercent => SelectedPool?.UsagePercent ?? 0d;
    /// <summary>获取当前使用率文本。</summary>
    public string SelectedUsagePercentText => SelectedPool?.UsagePercentText ?? "0%";
    /// <summary>获取当前池是否仍有借出对象，只作为疑似未归还提示。</summary>
    public bool SelectedHasActiveObjects => SelectedActiveCount > 0;
    /// <summary>获取当前池是否进入最近一次显式检查的疑似未归还候选。</summary>
    public bool SelectedIsLeakCandidate => SelectedPool?.IsLeakCandidate == true;
    /// <summary>获取当前池是否达到高压力阈值。</summary>
    public bool SelectedIsHighPressure => SelectedUsagePercent >= 90d;
    /// <summary>获取当前借出对象列表。</summary>
    public IReadOnlyList<PoolKitObjectListItemViewModel> SelectedActiveObjects => SelectedPool?.ActiveObjects ?? Array.Empty<PoolKitObjectListItemViewModel>();
    /// <summary>获取当前池内对象列表。</summary>
    public IReadOnlyList<WorkbenchPoolKitObject> SelectedInactiveObjects => SelectedPool?.InactiveObjects ?? Array.Empty<WorkbenchPoolKitObject>();
    /// <summary>获取借出对象总量文本。</summary>
    public string SelectedActiveObjectCountText => CreateObjectCountText(SelectedPool?.ActiveObjectTotal ?? 0, SelectedPool?.ActiveObjectTruncated == true);
    /// <summary>获取池内对象总量文本。</summary>
    public string SelectedInactiveObjectCountText => CreateObjectCountText(SelectedPool?.InactiveObjectTotal ?? 0, SelectedPool?.InactiveObjectTruncated == true);
    /// <summary>获取所选池事件数量文本。</summary>
    public string SelectedEventCountText => string.Format(
        GetString("String.PoolKit.SelectedEventCountTemplate", "{0} 条"), Events.Count);
    /// <summary>获取当前借出对象列表是否为空。</summary>
    public bool SelectedActiveObjectsEmpty => SelectedActiveObjects.Count == 0;
    /// <summary>获取当前池内对象列表是否为空。</summary>
    public bool SelectedInactiveObjectsEmpty => SelectedInactiveObjects.Count == 0;
    /// <summary>获取当前池事件流是否为空。</summary>
    public bool IsEventEmpty => Events.Count == 0;

    /// <summary>协调完整对象池集合并复用同身份行。</summary>
    private void ReconcilePools(WorkbenchPoolKitState state)
    {
        HashSet<string> retained = new(StringComparer.Ordinal);
        HashSet<string> leakPoolIds = new(StringComparer.Ordinal);
        HashSet<string> legacyLeakNames = new(StringComparer.Ordinal);
        foreach (WorkbenchPoolKitSuspectedLeak leak in state.Leaks.SuspectedLeaks)
        {
            if (string.IsNullOrWhiteSpace(leak.PoolId))
            {
                legacyLeakNames.Add(leak.PoolName);
            }
            else
            {
                leakPoolIds.Add(leak.PoolId);
            }
        }

        mAllPools.Clear();
        foreach (WorkbenchPoolKitPool pool in state.Pools)
        {
            retained.Add(pool.Identity);
            if (!mPoolsByIdentity.TryGetValue(pool.Identity, out PoolKitPoolListItemViewModel? row))
            {
                row = new PoolKitPoolListItemViewModel(pool, OpenCodeLocationAsync);
                mPoolsByIdentity.Add(pool.Identity, row);
            }

            bool isLeakCandidate = leakPoolIds.Contains(pool.StablePoolId)
                || legacyLeakNames.Contains(pool.Name);
            row.Update(pool, FindRecentEventText(pool, state.Events), isLeakCandidate);
            mAllPools.Add(row);
        }

        mStaleKeys.Clear();
        foreach (string identity in mPoolsByIdentity.Keys)
        {
            if (!retained.Contains(identity)) mStaleKeys.Add(identity);
        }
        foreach (string identity in mStaleKeys) mPoolsByIdentity.Remove(identity);

        ReconcileVisiblePools();
    }

    /// <summary>按搜索条件增量协调可见对象池集合。</summary>
    private void ReconcileVisiblePools()
    {
        mDesiredPools.Clear();
        foreach (PoolKitPoolListItemViewModel row in mAllPools)
        {
            if (row.Matches(SearchText)) mDesiredPools.Add(row);
        }

        ReconcilePoolCollection();
        RestorePoolSelection();
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoRuntimePools));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(SearchEmptyText));
    }

    /// <summary>通过 Move/Insert/Remove 更新列表，避免等价帧替换 ItemsSource。</summary>
    private void ReconcilePoolCollection()
    {
        for (var index = 0; index < mDesiredPools.Count; index++)
        {
            PoolKitPoolListItemViewModel desired = mDesiredPools[index];
            if (index < Pools.Count && ReferenceEquals(Pools[index], desired)) continue;
            int existingIndex = Pools.IndexOf(desired);
            if (existingIndex >= 0) Pools.Move(existingIndex, index);
            else Pools.Insert(index, desired);
        }

        while (Pools.Count > mDesiredPools.Count) Pools.RemoveAt(Pools.Count - 1);
    }

    /// <summary>保留仍可见选择，首次获得对象池时默认选择首项。</summary>
    private void RestorePoolSelection()
    {
        if (SelectedPool != null && Pools.Contains(SelectedPool))
        {
            ReconcileSelectedEvents();
            NotifySelectionProperties();
            return;
        }

        SelectedPool = Pools.Count > 0 ? Pools[0] : null;
    }

    /// <summary>清除可能隐藏检查结果的搜索条件，并选中首个疑似未归还候选池。</summary>
    private PoolKitPoolListItemViewModel? FocusFirstLeakCandidate()
    {
        if (!string.IsNullOrEmpty(SearchText)) SearchText = string.Empty;
        PoolKitPoolListItemViewModel? candidate = Pools.FirstOrDefault(static item => item.IsLeakCandidate);
        if (candidate != null) SelectedPool = candidate;
        return candidate;
    }

    /// <summary>协调当前池事件流并复用同身份行。</summary>
    private void ReconcileSelectedEvents()
    {
        mDesiredEvents.Clear();
        if (SelectedPool == null)
        {
            Events.Clear();
            OnPropertyChanged(nameof(SelectedEventCountText));
            OnPropertyChanged(nameof(IsEventEmpty));
            return;
        }

        Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
        HashSet<string> retained = new(StringComparer.Ordinal);
        foreach (WorkbenchPoolKitEvent item in mAllEvents)
        {
            if (!IsEventForPool(item, SelectedPool)) continue;
            string baseIdentity = item.Timestamp + "\u001f" + item.EventType + "\u001f" + item.PoolId
                + "\u001f" + item.PoolName + "\u001f" + item.ObjectName;
            int occurrence = occurrences.TryGetValue(baseIdentity, out int current) ? current : 0;
            occurrences[baseIdentity] = occurrence + 1;
            string identity = baseIdentity + "\u001f" + occurrence;
            retained.Add(identity);
            if (!mEventsByIdentity.TryGetValue(identity, out PoolKitEventListItemViewModel? row))
            {
                row = new PoolKitEventListItemViewModel(item, occurrence);
                mEventsByIdentity[identity] = row;
            }

            mDesiredEvents.Add(row);
        }

        mStaleKeys.Clear();
        foreach (string identity in mEventsByIdentity.Keys)
        {
            if (!retained.Contains(identity)) mStaleKeys.Add(identity);
        }
        foreach (string identity in mStaleKeys) mEventsByIdentity.Remove(identity);

        ReconcileEventCollection();

        OnPropertyChanged(nameof(SelectedEventCountText));
        OnPropertyChanged(nameof(IsEventEmpty));
    }

    /// <summary>增量协调事件行并保留等价事件对象身份。</summary>
    private void ReconcileEventCollection()
    {
        for (var index = 0; index < mDesiredEvents.Count; index++)
        {
            PoolKitEventListItemViewModel desired = mDesiredEvents[index];
            if (index < Events.Count && ReferenceEquals(Events[index], desired)) continue;
            int existingIndex = Events.IndexOf(desired);
            if (existingIndex >= 0) Events.Move(existingIndex, index);
            else Events.Insert(index, desired);
        }

        while (Events.Count > mDesiredEvents.Count) Events.RemoveAt(Events.Count - 1);
    }

    /// <summary>获取对象池最新事件的中文提示。</summary>
    private static string FindRecentEventText(WorkbenchPoolKitPool pool, IReadOnlyList<WorkbenchPoolKitEvent> events)
    {
        WorkbenchPoolKitEvent? item = events.FirstOrDefault(evt => IsEventForPool(evt, pool));
        return item?.EventType switch
        {
            "Spawn" => GetString("String.PoolKit.RecentSpawn", "刚借出"),
            "Return" => GetString("String.PoolKit.RecentReturn", "刚归还"),
            "Forced" => GetString("String.PoolKit.RecentForced", "强制归还"),
            _ => string.Empty
        };
    }

    /// <summary>优先使用稳定池标识关联事件，兼容旧 payload 的名称关联。</summary>
    private static bool IsEventForPool(WorkbenchPoolKitEvent item, PoolKitPoolListItemViewModel pool)
    {
        return IsEventForPool(item, pool.Pool);
    }

    /// <summary>优先使用稳定池标识关联事件，兼容旧 payload 的名称关联。</summary>
    private static bool IsEventForPool(WorkbenchPoolKitEvent item, WorkbenchPoolKitPool pool)
    {
        if (!string.IsNullOrWhiteSpace(item.PoolId) && !string.IsNullOrWhiteSpace(pool.PoolId))
        {
            return string.Equals(item.PoolId, pool.StablePoolId, StringComparison.Ordinal);
        }

        return string.Equals(item.PoolName, pool.Name, StringComparison.Ordinal);
    }

    /// <summary>创建带裁剪标记的对象数量文本。</summary>
    private static string CreateObjectCountText(int count, bool truncated) => count + (truncated ? "+" : string.Empty);

    /// <summary>通知所有当前选择派生字段。</summary>
    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelectionEmpty));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedTypeName));
        OnPropertyChanged(nameof(SelectedActiveCount));
        OnPropertyChanged(nameof(SelectedInactiveCount));
        OnPropertyChanged(nameof(SelectedTotalCount));
        OnPropertyChanged(nameof(SelectedPeakCount));
        OnPropertyChanged(nameof(SelectedMaxCacheCountText));
        OnPropertyChanged(nameof(SelectedUsagePercent));
        OnPropertyChanged(nameof(SelectedUsagePercentText));
        OnPropertyChanged(nameof(SelectedHasActiveObjects));
        OnPropertyChanged(nameof(SelectedIsLeakCandidate));
        OnPropertyChanged(nameof(SelectedIsHighPressure));
        OnPropertyChanged(nameof(SelectedActiveObjects));
        OnPropertyChanged(nameof(SelectedInactiveObjects));
        OnPropertyChanged(nameof(SelectedActiveObjectCountText));
        OnPropertyChanged(nameof(SelectedInactiveObjectCountText));
        OnPropertyChanged(nameof(SelectedActiveObjectsEmpty));
        OnPropertyChanged(nameof(SelectedInactiveObjectsEmpty));
    }
}
