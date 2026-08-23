using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.ViewModels.LogKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 LogKit 内存历史的稳定身份、筛选、选择和清空流程。</summary>
public sealed partial class LogKitPageViewModel
{
    private readonly Dictionary<HistoryRowKey, LogKitHistoryRowViewModel> mRowsByKey = new();
    private readonly Dictionary<WorkbenchLogKitHistoryEntry, int> mOccurrenceCounts = new();
    private readonly HashSet<HistoryRowKey> mRetainedRowKeys = new();
    private readonly List<HistoryRowKey> mRemovedRowKeys = new();
    private readonly List<LogKitHistoryRowViewModel> mAllHistoryRows = new();
    private readonly List<LogKitHistoryRowViewModel> mDesiredHistoryRows = new();
    private string mHistorySearchText = string.Empty;
    private string mSelectedHistoryLevel = HISTORY_LEVEL_ALL;
    private LogKitHistoryRowViewModel? mSelectedHistoryRow;
    private string mHistoryStatusText = string.Empty;

    /// <summary>获取或设置内存日志搜索文本。</summary>
    public string HistorySearchText
    {
        get => mHistorySearchText;
        set
        {
            if (SetProperty(ref mHistorySearchText, value ?? string.Empty))
            {
                ReconcileVisibleHistory();
            }
        }
    }

    /// <summary>获取或设置内存日志等级筛选。</summary>
    public string SelectedHistoryLevel
    {
        get => mSelectedHistoryLevel;
        set
        {
            if (SetProperty(ref mSelectedHistoryLevel, NormalizeHistoryLevel(value)))
            {
                ReconcileVisibleHistory();
            }
        }
    }

    /// <summary>获取或设置当前选中的内存日志。</summary>
    public LogKitHistoryRowViewModel? SelectedHistoryRow
    {
        get => mSelectedHistoryRow;
        set
        {
            if (SetProperty(ref mSelectedHistoryRow, value))
            {
                NotifySelectedHistoryProperties();
            }
        }
    }

    /// <summary>获取清空内存历史操作状态。</summary>
    public string HistoryStatusText
    {
        get => mHistoryStatusText;
        private set
        {
            if (SetProperty(ref mHistoryStatusText, value))
            {
                OnPropertyChanged(nameof(ActiveSourceStatusText));
            }
        }
    }
    /// <summary>获取筛选结果与 Runtime 返回窗口总量。</summary>
    public string VisibleHistoryCountText => HistoryRows.Count + " / " + mAllHistoryRows.Count;
    /// <summary>获取当前是否有可见日志。</summary>
    public bool HasVisibleHistory => HistoryRows.Count > 0;
    /// <summary>获取当前是否应显示内存日志空状态。</summary>
    public bool IsHistoryEmpty => !HasVisibleHistory;
    /// <summary>获取是否已选择一条内存日志。</summary>
    public bool HasSelectedHistory => SelectedHistoryRow != null;
    /// <summary>获取详情区等级。</summary>
    public string SelectedHistoryLevelText => SelectedHistoryRow?.LevelText
        ?? GetString("String.LogKit.NoLogSelected", "未选择日志");
    /// <summary>获取详情区本地时间。</summary>
    public string SelectedHistoryTimeText => SelectedHistoryRow?.TimeText ?? "--";
    /// <summary>获取详情区消息。</summary>
    public string SelectedHistoryMessageText => SelectedHistoryRow?.MessageText
        ?? GetString("String.LogKit.NoLogRecordSelected", "未选择日志记录");
    /// <summary>获取详情区上下文。</summary>
    public string SelectedHistoryContextText => SelectedHistoryRow?.Entry.Context ?? string.Empty;
    /// <summary>获取详情区异常摘要。</summary>
    public string SelectedHistoryExceptionText => SelectedHistoryRow?.ExceptionSummary ?? string.Empty;
    /// <summary>获取详情区堆栈。</summary>
    public string SelectedHistoryStackTraceText => SelectedHistoryRow?.Entry.StackTrace ?? string.Empty;
    /// <summary>获取所选日志是否包含上下文。</summary>
    public bool SelectedHistoryHasContext => SelectedHistoryRow?.HasContext == true;
    /// <summary>获取所选日志是否包含异常或堆栈。</summary>
    public bool SelectedHistoryHasException => SelectedHistoryRow?.HasException == true;

    /// <summary>按完整记录和重复序号复用行对象，并删除当前帧已不存在的对象。</summary>
    private void ReconcileHistory(IReadOnlyList<WorkbenchLogKitHistoryEntry> entries)
    {
        mOccurrenceCounts.Clear();
        mRetainedRowKeys.Clear();
        mDesiredHistoryRows.Clear();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var occurrence = NextOccurrence(entry);
            HistoryRowKey key = new(entry, occurrence);
            if (!mRowsByKey.TryGetValue(key, out var row))
            {
                row = new LogKitHistoryRowViewModel(CreateRowIdentity(entry, occurrence), entry);
                mRowsByKey.Add(key, row);
            }

            mRetainedRowKeys.Add(key);
            mDesiredHistoryRows.Add(row);
        }

        RemoveExpiredHistoryRows();
        ReplaceAllHistoryRows();
        ReconcileVisibleHistory();
    }

    /// <summary>返回同内容日志在当前帧内从零开始的重复序号。</summary>
    private int NextOccurrence(WorkbenchLogKitHistoryEntry entry)
    {
        if (!mOccurrenceCounts.TryGetValue(entry, out var occurrence))
        {
            mOccurrenceCounts.Add(entry, 1);
            return 0;
        }

        mOccurrenceCounts[entry] = occurrence + 1;
        return occurrence;
    }

    /// <summary>删除当前帧不再引用的稳定行，控制长期字典规模。</summary>
    private void RemoveExpiredHistoryRows()
    {
        mRemovedRowKeys.Clear();
        foreach (var key in mRowsByKey.Keys)
        {
            if (!mRetainedRowKeys.Contains(key))
            {
                mRemovedRowKeys.Add(key);
            }
        }

        foreach (var key in mRemovedRowKeys)
        {
            mRowsByKey.Remove(key);
        }
    }

    /// <summary>用复用后的目标顺序更新内部完整窗口。</summary>
    private void ReplaceAllHistoryRows()
    {
        mAllHistoryRows.Clear();
        mAllHistoryRows.AddRange(mDesiredHistoryRows);
    }

    /// <summary>增量协调筛选集合，保留仍存在行的引用、位置和选择。</summary>
    private void ReconcileVisibleHistory()
    {
        mDesiredHistoryRows.Clear();
        foreach (var row in mAllHistoryRows)
        {
            if (row.Matches(HistorySearchText, SelectedHistoryLevel))
            {
                mDesiredHistoryRows.Add(row);
            }
        }

        ReconcileVisibleCollection();
        RestoreHistorySelection();
        NotifyHistorySummaryProperties();
    }

    /// <summary>通过 Move/Insert/Remove 更新 ObservableCollection，避免等价帧触发整表重建。</summary>
    private void ReconcileVisibleCollection()
    {
        for (var index = 0; index < mDesiredHistoryRows.Count; index++)
        {
            var desired = mDesiredHistoryRows[index];
            if (index < HistoryRows.Count && ReferenceEquals(HistoryRows[index], desired))
            {
                continue;
            }

            var existingIndex = HistoryRows.IndexOf(desired);
            if (existingIndex >= 0)
            {
                HistoryRows.Move(existingIndex, index);
            }
            else
            {
                HistoryRows.Insert(index, desired);
            }
        }

        while (HistoryRows.Count > mDesiredHistoryRows.Count)
        {
            HistoryRows.RemoveAt(HistoryRows.Count - 1);
        }
    }

    /// <summary>保留仍可见的选择；首次获得日志时默认选中第一条。</summary>
    private void RestoreHistorySelection()
    {
        if (SelectedHistoryRow != null && HistoryRows.Contains(SelectedHistoryRow))
        {
            NotifySelectedHistoryProperties();
            return;
        }

        SelectedHistoryRow = HistoryRows.Count > 0 ? HistoryRows[0] : null;
    }

    /// <summary>清空 Runtime 历史，并只接受仍属于当前宿主代的异步结果。</summary>
    private async Task ClearHistoryAsync()
    {
        if (mClearHistoryAsync == null || string.IsNullOrWhiteSpace(EngineId))
        {
            return;
        }

        var identity = CaptureIdentity();
        var token = mIdentityCancellation.Token;
        HistoryStatusText = GetString("String.LogKit.ClearingHistory", "正在清空内存历史...");
        try
        {
            var state = await mClearHistoryAsync(EngineId, token);
            if (MatchesIdentity(identity.EngineId, identity.SessionId, identity.Generation))
            {
                ApplyState(state, false);
                HistoryStatusText = GetString("String.LogKit.HistoryCleared", "内存历史已清空");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HistoryStatusText = string.Format(
                GetString("String.LogKit.ClearFailedTemplate", "清空失败: {0}"), exception.Message);
        }
    }

    /// <summary>判断当前是否具备清空 Runtime 历史的稳定身份和 Application 用例。</summary>
    private bool CanClearHistory()
    {
        return !mIsDisposed && mClearHistoryAsync != null && !string.IsNullOrWhiteSpace(EngineId);
    }

    /// <summary>在历史数量变化后刷新清空命令状态。</summary>
    private void NotifyHistorySummaryProperties()
    {
        OnPropertyChanged(nameof(VisibleHistoryCountText));
        OnPropertyChanged(nameof(HasVisibleHistory));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        OnPropertyChanged(nameof(ActiveSourceCountText));
        ClearHistoryCommand.RaiseCanExecuteChanged();
    }

    /// <summary>通知详情区所有派生字段，保证一次选择形成完整视图。</summary>
    private void NotifySelectedHistoryProperties()
    {
        OnPropertyChanged(nameof(HasSelectedHistory));
        OnPropertyChanged(nameof(SelectedHistoryLevelText));
        OnPropertyChanged(nameof(SelectedHistoryTimeText));
        OnPropertyChanged(nameof(SelectedHistoryMessageText));
        OnPropertyChanged(nameof(SelectedHistoryContextText));
        OnPropertyChanged(nameof(SelectedHistoryExceptionText));
        OnPropertyChanged(nameof(SelectedHistoryStackTraceText));
        OnPropertyChanged(nameof(SelectedHistoryHasContext));
        OnPropertyChanged(nameof(SelectedHistoryHasException));
    }

    /// <summary>为新行创建仅一次分配的可测试身份。</summary>
    private static string CreateRowIdentity(WorkbenchLogKitHistoryEntry entry, int occurrence)
    {
        return entry.TimestampUtc + "\u001f" + entry.Level + "\u001f" + entry.Message + "\u001f" + occurrence;
    }

    /// <summary>捕获异步操作开始时的宿主身份。</summary>
    private HostIdentity CaptureIdentity()
    {
        return new HostIdentity(EngineId, SessionId, Generation);
    }

    /// <summary>把界面传入的等级值归一化为不随语言变化的哨兵或原始等级。</summary>
    /// <param name="value">界面传入的等级展示值（“全部”/"All"）或具体等级。</param>
    /// <returns>归一化后的内部筛选值。</returns>
    private static string NormalizeHistoryLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return HISTORY_LEVEL_ALL;
        }

        return value.Trim() switch
        {
            HISTORY_LEVEL_ALL => HISTORY_LEVEL_ALL,
            "全部" => HISTORY_LEVEL_ALL,
            "All" => HISTORY_LEVEL_ALL,
            _ => value.Trim()
        };
    }

    /// <summary>使用完整日志值和重复序号标识帧内唯一行。</summary>
    private readonly record struct HistoryRowKey(WorkbenchLogKitHistoryEntry Entry, int Occurrence);

    /// <summary>保存异步操作开始时的宿主三元身份。</summary>
    private readonly record struct HostIdentity(string EngineId, string SessionId, long Generation);
}
