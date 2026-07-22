using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Workbench.Avalonia.ViewModels.EventKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 EventKit 页面高频集合的无临时数组协调逻辑。</summary>
public sealed partial class EventKitPageViewModel
{
    /// <summary>按 identity 更新现有项、创建新增项并删除失效项。</summary>
    private void ReconcileEventItems(
        IReadOnlyList<WorkbenchEventKitEvent> events,
        bool refreshStaticRelations = false)
    {
        mRuntimeEventsByIdentity.Clear();
        for (var index = 0; index < events.Count; index++)
        {
            mRuntimeEventsByIdentity[events[index].Identity] = events[index];
        }

        mRetainedIdentities.Clear();
        foreach ((string identity, WorkbenchEventKitEvent runtime) in mRuntimeEventsByIdentity)
        {
            mRetainedIdentities.Add(identity);
            if (!mItemsByIdentity.TryGetValue(identity, out EventKitEventListItemViewModel? item))
            {
                item = new EventKitEventListItemViewModel(
                    runtime,
                    mOpenLocationAsync == null ? null : OpenCodeLocationAsync);
                mItemsByIdentity.Add(identity, item);
            }

            item.Apply(runtime);
            if (mCodeRelationsByIdentity.TryGetValue(identity, out WorkbenchEventKitCodeRelation? relation))
            {
                if (refreshStaticRelations || !item.HasStaticRelation)
                {
                    item.Apply(relation);
                }
            }
            else
            {
                item.ClearStaticRelation();
            }
        }

        foreach ((string identity, WorkbenchEventKitCodeRelation relation) in mCodeRelationsByIdentity)
        {
            mRetainedIdentities.Add(identity);
            if (mRuntimeEventsByIdentity.ContainsKey(identity))
            {
                continue;
            }

            if (!mItemsByIdentity.TryGetValue(identity, out EventKitEventListItemViewModel? item))
            {
                item = new EventKitEventListItemViewModel(
                    relation,
                    mOpenLocationAsync == null ? null : OpenCodeLocationAsync);
                mItemsByIdentity.Add(identity, item);
            }

            item.ClearRuntime();
            if (refreshStaticRelations || !item.HasStaticRelation)
            {
                item.Apply(relation);
            }
        }

        mRemovedIdentities.Clear();
        foreach (string identity in mItemsByIdentity.Keys)
        {
            if (!mRetainedIdentities.Contains(identity))
            {
                mRemovedIdentities.Add(identity);
            }
        }

        for (var index = 0; index < mRemovedIdentities.Count; index++)
        {
            mItemsByIdentity.Remove(mRemovedIdentities[index]);
        }
    }

    /// <summary>按当前搜索与通道筛选增量协调可见集合顺序。</summary>
    private void ReconcileVisibleEvents()
    {
        mDesiredEvents.Clear();
        foreach (EventKitEventListItemViewModel item in mItemsByIdentity.Values)
        {
            if (MatchesFilter(item))
            {
                mDesiredEvents.Add(item);
            }
        }

        mDesiredEvents.Sort(CompareEventItems);
        ReconcileCollection(Events, mDesiredEvents, mDesiredEventSet);
        if (SelectedEvent != null && !Events.Contains(SelectedEvent))
        {
            SelectedEvent = Events.Count == 0 ? null : Events[0];
        }

        OnPropertyChanged(nameof(VisibleCountText));
    }

    /// <summary>判断事件是否匹配通道和自由文本搜索。</summary>
    private bool MatchesFilter(EventKitEventListItemViewModel item)
    {
        if (!string.Equals(SelectedChannel, "全部", StringComparison.Ordinal)
            && !string.Equals(item.Channel, SelectedChannel, StringComparison.Ordinal))
        {
            return false;
        }

        string query = SearchText.Trim();
        return query.Length == 0
            || Contains(item.Channel, query)
            || Contains(item.EventKey, query)
            || Contains(item.PayloadType, query)
            || MatchesLocation(item.Senders, query)
            || MatchesLocation(item.Receivers, query)
            || MatchesLocation(item.Unregisters, query);
    }

    /// <summary>判断源码位置列表是否包含搜索词。</summary>
    private static bool MatchesLocation(
        IReadOnlyList<EventKitCodeLocationItemViewModel> locations,
        string query)
    {
        for (var index = 0; index < locations.Count; index++)
        {
            if (Contains(locations[index].FilePath, query))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>恢复同 identity 选择，缺失时选择第一项。</summary>
    private void RestoreSelection(string selectedIdentity)
    {
        EventKitEventListItemViewModel? preferred = null;
        for (var index = 0; index < Events.Count; index++)
        {
            if (string.Equals(Events[index].Identity, selectedIdentity, StringComparison.Ordinal))
            {
                preferred = Events[index];
                break;
            }
        }

        EventKitEventListItemViewModel? next = preferred ?? (Events.Count == 0 ? null : Events[0]);
        if (ReferenceEquals(SelectedEvent, next))
        {
            ReconcileSelectedActivities();
        }
        else
        {
            SelectedEvent = next;
        }
    }

    /// <summary>只协调当前事件的时间线，保持既有 record 引用和滚动位置。</summary>
    private void ReconcileSelectedActivities()
    {
        if (SelectedEvent == null)
        {
            SelectedActivities.Clear();
            NotifySelectionProperties();
            return;
        }

        mActivitiesBySequence.Clear();
        for (var index = 0; index < SelectedActivities.Count; index++)
        {
            mActivitiesBySequence[SelectedActivities[index].Sequence] = SelectedActivities[index];
        }

        mDesiredActivities.Clear();
        for (var index = mAllActivities.Count - 1; index >= 0; index--)
        {
            WorkbenchEventKitActivity activity = mAllActivities[index];
            if (!MatchesSelectedActivity(activity))
            {
                continue;
            }

            mDesiredActivities.Add(mActivitiesBySequence.TryGetValue(activity.Sequence, out var current)
                ? current
                : activity);
        }

        ReconcileCollection(SelectedActivities, mDesiredActivities, mDesiredActivitySet);
        NotifySelectionProperties();
    }

    /// <summary>判断活动是否属于当前事件；clear 可覆盖指定事件键或整个通道。</summary>
    private bool MatchesSelectedActivity(WorkbenchEventKitActivity activity)
    {
        if (SelectedEvent == null
            || !string.Equals(activity.Channel, SelectedEvent.Channel, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(activity.Kind, "clear", StringComparison.Ordinal))
        {
            return string.Equals(activity.EventKey, "*", StringComparison.Ordinal)
                || string.Equals(activity.EventKey, SelectedEvent.EventKey, StringComparison.Ordinal);
        }

        return string.Equals(activity.Identity, SelectedEvent.Identity, StringComparison.Ordinal);
    }

    /// <summary>按 desired 顺序执行最小增删移动，避免 Clear 导致选中态闪烁。</summary>
    private static void ReconcileCollection<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> desired,
        HashSet<T> desiredSet)
        where T : class
    {
        desiredSet.Clear();
        for (var index = 0; index < desired.Count; index++)
        {
            desiredSet.Add(desired[index]);
        }

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredSet.Contains(target[index]))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            T item = desired[index];
            if (index < target.Count && ReferenceEquals(target[index], item))
            {
                continue;
            }

            int existingIndex = target.IndexOf(item);
            if (existingIndex < 0)
            {
                target.Insert(index, item);
            }
            else
            {
                target.Move(existingIndex, index);
            }
        }
    }

    /// <summary>统计当前选择指定 kind 的活动数量。</summary>
    private int CountSelectedActivities(string kind)
    {
        var count = 0;
        for (var index = 0; index < SelectedActivities.Count; index++)
        {
            if (string.Equals(SelectedActivities[index].Kind, kind, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>按通道、事件键和负载类型比较事件列表项。</summary>
    private static int CompareEventItems(
        EventKitEventListItemViewModel left,
        EventKitEventListItemViewModel right)
    {
        int channel = GetChannelRank(left.Channel).CompareTo(GetChannelRank(right.Channel));
        if (channel != 0)
        {
            return channel;
        }

        int eventKey = string.Compare(left.EventKey, right.EventKey, StringComparison.OrdinalIgnoreCase);
        return eventKey != 0
            ? eventKey
            : string.Compare(left.PayloadType, right.PayloadType, StringComparison.OrdinalIgnoreCase);
    }
}
