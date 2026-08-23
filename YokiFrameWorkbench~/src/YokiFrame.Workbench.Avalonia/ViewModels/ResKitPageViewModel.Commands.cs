using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels.ResKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ResKit 状态应用、按需详情和显式诊断命令。</summary>
public sealed partial class ResKitPageViewModel
{
    /// <summary>应用同宿主新 state、更新页面事实和稳定集合。</summary>
    private void ApplyState(WorkbenchResKitState state)
    {
        bool hostChanged = !MatchesIdentity(state);
        string selectedIdentity = SelectedResource?.Identity ?? string.Empty;
        long selectedProviderGeneration = SelectedResource?.ProviderGeneration ?? 0L;
        mEngineId = state.EngineId;
        mSessionId = state.SessionId;
        mGeneration = state.Generation;
        mVersion = state.Version;
        mHasRuntimeState = true;
        Source = state.Source;
        StaleReason = state.StaleReason;
        mLastBackgroundFailure = state.LastBackgroundFailure;
        HistoryTotal = state.HistoryTotal;
        HistoryDroppedCount = state.HistoryDroppedCount;
        TrackingEnabled = state.Stats.LoadLocationTrackingEnabled;
        ResourcesTruncated = state.ResourcesTruncated;
        HistoryTruncated = state.HistoryTruncated;
        mResourceTotal = state.ResourceTotal;
        ReconcileResources(state);
        ReconcileHistory(state);
        bool selectedResourceChanged = hostChanged
            || SelectedResource == null
            || SelectedResource.Identity != selectedIdentity
            || SelectedResource.ProviderGeneration != selectedProviderGeneration;
        if (selectedResourceChanged)
        {
            ShowSelectedSourcePreview();
        }
        else if (mSourceDetailVersion == 0L)
        {
            ShowSelectedSourcePreview();
        }
        else if (Sources.Count > 0 && mSourceDetailVersion != state.Version)
        {
            SourceStatusText = string.Format(
                GetString(SourceStaleAfterReadTemplateKey, "来源读取于 v{0}，当前 v{1}，可重新读取"),
                mSourceDetailVersion, state.Version);
        }
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(HasGlobalWarning));
        OnPropertyChanged(nameof(GlobalWarningText));
        NotifyResourceEmptyState();
        RaiseCommandStates();
    }

    /// <summary>断线后清空运行事实和列表，不保留容易误导的旧资源详情。</summary>
    private void ResetRuntimeState()
    {
        mEngineId = string.Empty;
        mSessionId = string.Empty;
        mGeneration = 0L;
        mVersion = 0L;
        mHasRuntimeState = false;
        Source = GetString(CommonWaitingForKey, "等待数据");
        StaleReason = string.Empty;
        mLastBackgroundFailure = string.Empty;
        HistoryTotal = 0;
        HistoryDroppedCount = 0;
        TrackingEnabled = false;
        ResourcesTruncated = false;
        HistoryTruncated = false;
        mResourceTotal = 0;
        mAllResources.Clear();
        mResourcesByIdentity.Clear();
        mHistoryByIdentity.Clear();
        Resources.Clear();
        History.Clear();
        SelectedResource = null;
        ClearSources(GetString(WaitingStateKey, "等待 ResKit 状态"));
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(IsResourceEmpty));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        OnPropertyChanged(nameof(HasGlobalWarning));
        OnPropertyChanged(nameof(GlobalWarningText));
        NotifyResourceEmptyState();
        RaiseCommandStates();
    }

    /// <summary>显式读取当前资源的独立 lease 来源，并拒绝覆盖已观察到的更新版本。</summary>
    private async Task LoadSourcesAsync()
    {
        ResKitResourceListItemViewModel? selected = SelectedResource;
        if (mGetDetailAsync == null || selected == null) return;
        string requestEngineId = mEngineId;
        string requestSessionId = mSessionId;
        long requestGeneration = mGeneration;
        string requestIdentity = selected.Identity;
        long requestProviderGeneration = selected.ProviderGeneration;
        SourceStatusText = GetString(ReadingSourcesKey, "正在读取 lease 来源...");
        IsSourceLoading = true;
        try
        {
            WorkbenchResKitResourceDetail result = await mGetDetailAsync(
                requestEngineId, selected.Path, selected.TypeName, mLifetimeCancellation.Token);
            WorkbenchResKitResource detail = result.Resource;
            if (SelectedResource?.Identity != requestIdentity
                || detail.Identity != requestIdentity
                || SelectedResource.ProviderGeneration != requestProviderGeneration
                || detail.ProviderGeneration != requestProviderGeneration
                || mEngineId != requestEngineId
                || mSessionId != requestSessionId
                || mGeneration != requestGeneration) return;
            if (result.Version < mVersion)
            {
                SourceStatusText = string.Format(
                    GetString(SourceStaleKeptTemplateKey, "来源读取于 v{0}，当前状态已更新至 v{1}，已保留实时预览"),
                    result.Version, mVersion);
                return;
            }

            Sources.Clear();
            foreach (WorkbenchResKitLoadSource source in detail.Sources)
            {
                Sources.Add(new ResKitLoadSourceItemViewModel(source, mOpenLocationAsync));
            }
            mSourceDetailVersion = result.Version;
            SourceStatusText = detail.SourcesTruncated
                ? string.Format(
                    GetString(SourceShownPartialTemplateKey, "v{0} · 已显示 {1} / {2} 条来源"),
                    result.Version, Sources.Count, detail.SourceTotal)
                : string.Format(
                    GetString(SourceReadAllTemplateKey, "v{0} · 已读取 {1} 条来源"),
                    result.Version, Sources.Count);
            OnPropertyChanged(nameof(SourceCountText));
            OnPropertyChanged(nameof(IsSourceEmpty));
            OnPropertyChanged(nameof(ShowSourceEmpty));
            OnPropertyChanged(nameof(SourceEmptyText));
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            SourceStatusText = Sources.Count > 0
                ? string.Format(
                    GetString(SourceReadFailedKeptTemplateKey, "读取失败: {0} · 已保留实时预览"), exception.Message)
                : string.Format(GetString(SourceReadFailedTemplateKey, "读取失败: {0}"), exception.Message);
        }
        finally { IsSourceLoading = false; }
    }

    /// <summary>切换加载位置跟踪并接受同一宿主返回的新 state。</summary>
    private async Task ToggleTrackingAsync()
    {
        if (mSetTrackingAsync == null) return;
        bool enabled = !TrackingEnabled;
        OperationStatusText = GetString(UpdatingTrackingKey, "正在更新加载位置跟踪...");
        try
        {
            ApplyState(await mSetTrackingAsync(mEngineId, enabled, mLifetimeCancellation.Token));
            OperationStatusText = enabled
                ? GetString(TrackingEnabledTextKey, "已启用加载位置跟踪，新 lease 将记录来源")
                : GetString(TrackingDisabledTextKey, "已关闭加载位置跟踪");
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            OperationStatusText = string.Format(
                GetString(UpdateFailedTemplateKey, "更新失败: {0}"), exception.Message);
        }
    }

    /// <summary>清空卸载历史并保留当前资源选择。</summary>
    private async Task ClearHistoryAsync()
    {
        if (mClearHistoryAsync == null) return;
        OperationStatusText = GetString(ClearingHistoryKey, "正在清空卸载历史...");
        try
        {
            ApplyState(await mClearHistoryAsync(mEngineId, mLifetimeCancellation.Token));
            OperationStatusText = GetString(HistoryClearedKey, "卸载历史已清空");
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            OperationStatusText = string.Format(
                GetString(ClearFailedTemplateKey, "清空失败: {0}"), exception.Message);
        }
    }

    /// <summary>判断当前是否可读取来源。</summary>
    private bool CanLoadSources() => mGetDetailAsync != null && SelectedResource != null && !string.IsNullOrWhiteSpace(mEngineId);
    /// <summary>判断当前是否可修改跟踪开关。</summary>
    private bool CanSetTracking() => mSetTrackingAsync != null && !string.IsNullOrWhiteSpace(mEngineId);
    /// <summary>判断当前是否可清空卸载历史。</summary>
    private bool CanClearHistory() => mClearHistoryAsync != null && !string.IsNullOrWhiteSpace(mEngineId);

    /// <summary>通知全部诊断命令重新计算可执行状态。</summary>
    private void RaiseCommandStates()
    {
        LoadSourcesCommand.RaiseCanExecuteChanged();
        ToggleTrackingCommand.RaiseCanExecuteChanged();
        ClearHistoryCommand.RaiseCanExecuteChanged();
    }

    /// <summary>来源读取落后于当前版本的提示模板资源 key。</summary>
    private const string SourceStaleAfterReadTemplateKey = "String.ResKit.SourceStaleAfterReadTemplate";

    /// <summary>等待 ResKit 状态占位资源 key。</summary>
    private const string WaitingStateKey = "String.ResKit.WaitingState";

    /// <summary>正在读取来源提示资源 key。</summary>
    private const string ReadingSourcesKey = "String.ResKit.ReadingSources";

    /// <summary>来源读取后状态更新的保留预览模板资源 key。</summary>
    private const string SourceStaleKeptTemplateKey = "String.ResKit.SourceStaleKeptTemplate";

    /// <summary>部分显示模板资源 key。</summary>
    private const string SourceShownPartialTemplateKey = "String.ResKit.SourceShownPartialTemplate";

    /// <summary>完整读取模板资源 key。</summary>
    private const string SourceReadAllTemplateKey = "String.ResKit.SourceReadAllTemplate";

    /// <summary>读取失败模板资源 key。</summary>
    private const string SourceReadFailedTemplateKey = "String.ResKit.SourceReadFailedTemplate";

    /// <summary>读取失败但保留预览模板资源 key。</summary>
    private const string SourceReadFailedKeptTemplateKey = "String.ResKit.SourceReadFailedKeptTemplate";

    /// <summary>正在更新定位跟踪提示资源 key。</summary>
    private const string UpdatingTrackingKey = "String.ResKit.UpdatingTracking";

    /// <summary>启用定位跟踪结果文案资源 key。</summary>
    private const string TrackingEnabledTextKey = "String.ResKit.TrackingEnabledText";

    /// <summary>关闭定位跟踪结果文案资源 key。</summary>
    private const string TrackingDisabledTextKey = "String.ResKit.TrackingDisabledText";

    /// <summary>更新失败模板资源 key。</summary>
    private const string UpdateFailedTemplateKey = "String.ResKit.UpdateFailedTemplate";

    /// <summary>正在清空卸载历史提示资源 key。</summary>
    private const string ClearingHistoryKey = "String.ResKit.ClearingHistory";

    /// <summary>卸载历史已清空文案资源 key。</summary>
    private const string HistoryClearedKey = "String.ResKit.HistoryCleared";

    /// <summary>清空失败模板资源 key。</summary>
    private const string ClearFailedTemplateKey = "String.ResKit.ClearFailedTemplate";
}
