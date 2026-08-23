using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 PoolKit 显式诊断命令和状态应用。</summary>
public sealed partial class PoolKitPageViewModel
{
    /// <summary>应用同宿主新 state、更新聚合指标和稳定集合。</summary>
    private void ApplyState(WorkbenchPoolKitState state)
    {
        mEngineId = state.EngineId;
        mSessionId = state.SessionId;
        mGeneration = state.Generation;
        mVersion = state.Version;
        Source = state.Source;
        StaleReason = state.StaleReason;
        PoolTotal = state.PoolTotal;
        EventTotal = state.EventTotal;
        TotalActive = state.Stats.TotalActive;
        LeakCount = state.Leaks.Total;
        TrackingEnabled = state.Stats.TrackingEnabled;
        StackTraceEnabled = state.Stats.StackTraceEnabled;
        EventHistoryEnabled = state.Stats.EventHistoryEnabled;
        PoolsTruncated = state.PoolsTruncated;
        EventsTruncated = state.EventsTruncated;
        LeaksTruncated = state.Leaks.Truncated;
        mAllEvents.Clear();
        mAllEvents.AddRange(state.Events);
        ReconcilePools(state);
        OnPropertyChanged(nameof(IsWaitingForData));
        OnPropertyChanged(nameof(LeakWarningText));
        OnPropertyChanged(nameof(HasLeakWarning));
        RaiseCommandStates();
    }

    /// <summary>断线后清空运行事实和列表，不保留容易误导的旧池详情。</summary>
    private void ResetRuntimeState()
    {
        mEngineId = string.Empty;
        mSessionId = string.Empty;
        mGeneration = 0L;
        mVersion = 0L;
        Source = GetString(CommonWaitingForKey, "等待数据");
        StaleReason = string.Empty;
        PoolTotal = 0;
        EventTotal = 0;
        TotalActive = 0;
        LeakCount = 0;
        TrackingEnabled = false;
        StackTraceEnabled = false;
        EventHistoryEnabled = false;
        PoolsTruncated = false;
        EventsTruncated = false;
        LeaksTruncated = false;
        mAllPools.Clear();
        mAllEvents.Clear();
        Pools.Clear();
        Events.Clear();
        SelectedPool = null;
        OnPropertyChanged(nameof(IsWaitingForData));
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoRuntimePools));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(SearchEmptyText));
        OnPropertyChanged(nameof(LeakWarningText));
        OnPropertyChanged(nameof(HasLeakWarning));
        RaiseCommandStates();
    }

    /// <summary>切换对象跟踪和事件历史；关闭时同步关闭高成本定位。</summary>
    private Task ToggleTrackingAsync()
    {
        bool nextTracking = !TrackingEnabled;
        return RunTrackingCommandAsync(nextTracking, nextTracking, nextTracking && StackTraceEnabled);
    }

    /// <summary>切换堆栈定位；开启时自动启用对象跟踪和事件历史。</summary>
    private Task ToggleLocationAsync()
    {
        bool nextStackTrace = !StackTraceEnabled;
        return RunTrackingCommandAsync(
            nextStackTrace || TrackingEnabled,
            nextStackTrace || EventHistoryEnabled,
            nextStackTrace);
    }

    /// <summary>执行跟踪配置命令并接受同一宿主返回的新 state。</summary>
    private async Task RunTrackingCommandAsync(bool tracking, bool events, bool stackTrace)
    {
        if (mSetTrackingAsync == null) return;
        OperationStatusText = GetString("String.PoolKit.UpdatingToggles", "正在更新诊断开关...");
        try
        {
            WorkbenchPoolKitState state = await mSetTrackingAsync(
                mEngineId, tracking, events, stackTrace, mLifetimeCancellation.Token);
            ApplyState(state);
            OperationStatusText = stackTrace
                ? GetString("String.PoolKit.EnabledStackTrace", "已启用堆栈定位")
                : (tracking
                    ? GetString("String.PoolKit.EnabledTracking", "已启用对象跟踪")
                    : GetString("String.PoolKit.StoppedTracking", "已停止对象跟踪"));
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            OperationStatusText = string.Format(
                GetString("String.PoolKit.UpdateFailedTemplate", "更新失败: {0}"), exception.Message);
        }
    }

    /// <summary>执行疑似未归还对象检查。</summary>
    private async Task CheckLeaksAsync()
    {
        if (mCheckLeaksAsync == null) return;
        OperationStatusText = GetString("String.PoolKit.CheckingBorrowedObjects", "正在检查借出对象...");
        try
        {
            ApplyState(await mCheckLeaksAsync(mEngineId, mLifetimeCancellation.Token));
            PoolKitPoolListItemViewModel? candidate = FocusFirstLeakCandidate();
            OperationStatusText = candidate == null
                ? GetString("String.PoolKit.NoBorrowedObjects", "未发现仍借出的对象")
                : string.Format(
                    GetString("String.PoolKit.LeaksLocatedTemplate", "发现 {0} 个候选池，已定位到 {1}"),
                    LeakCount, candidate.Name);
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            OperationStatusText = string.Format(
                GetString("String.PoolKit.CheckFailedTemplate", "检查失败: {0}"), exception.Message);
        }
    }

    /// <summary>通过宿主打开借出对象源码位置，并把成功或失败反馈到页头状态。</summary>
    private async Task OpenCodeLocationAsync(WorkbenchPoolKitObject item)
    {
        if (mOpenCodeLocationAsync == null || !item.HasSourceLocation) return;
        try
        {
            await mOpenCodeLocationAsync(item.SourceFile, item.SourceLine);
            OperationStatusText = string.Format(
                GetString("String.PoolKit.OpenedTemplate", "已打开 {0}"),
                Path.GetFileName(item.SourceFile) + ":" + item.SourceLine);
        }
        catch (Exception exception)
        {
            OperationStatusText = string.Format(
                GetString("String.PoolKit.LocateFailedTemplate", "定位失败: {0}"), exception.Message);
        }
    }

    /// <summary>清空事件历史并保留当前对象池选择。</summary>
    private async Task ClearHistoryAsync()
    {
        if (mClearHistoryAsync == null) return;
        OperationStatusText = GetString("String.PoolKit.ClearingEventHistory", "正在清空事件历史...");
        try
        {
            ApplyState(await mClearHistoryAsync(mEngineId, mLifetimeCancellation.Token));
            OperationStatusText = GetString("String.PoolKit.EventHistoryCleared", "事件历史已清空");
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            OperationStatusText = string.Format(
                GetString("String.PoolKit.ClearFailedTemplate", "清空失败: {0}"), exception.Message);
        }
    }

    /// <summary>判断当前是否可修改诊断开关。</summary>
    private bool CanSetTracking() => mSetTrackingAsync != null && !string.IsNullOrWhiteSpace(mEngineId);
    /// <summary>判断当前是否可执行泄漏检查。</summary>
    private bool CanCheckLeaks() => mCheckLeaksAsync != null && !string.IsNullOrWhiteSpace(mEngineId);
    /// <summary>判断当前是否可清空事件历史。</summary>
    private bool CanClearHistory() => mClearHistoryAsync != null && !string.IsNullOrWhiteSpace(mEngineId);

    /// <summary>通知全部诊断命令重新计算可执行状态。</summary>
    private void RaiseCommandStates()
    {
        ToggleTrackingCommand.RaiseCanExecuteChanged();
        ToggleLocationCommand.RaiseCanExecuteChanged();
        CheckLeaksCommand.RaiseCanExecuteChanged();
        ClearHistoryCommand.RaiseCanExecuteChanged();
    }
}
