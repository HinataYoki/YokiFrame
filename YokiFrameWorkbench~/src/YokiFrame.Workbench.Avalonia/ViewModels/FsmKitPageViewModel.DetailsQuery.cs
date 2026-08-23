using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 FsmKit 一次性详情查询的取消、身份校验和提交生命周期。</summary>
public sealed partial class FsmKitPageViewModel
{
    /// <summary>按稳定 instanceId 执行显式详情查询；失败转换为页面诊断文本。</summary>
    /// <param name="instanceId">FsmKit 注册表返回的稳定实例标识。</param>
    /// <returns>详情查询完成任务。</returns>
    public Task QueryInstanceAsync(string instanceId)
    {
        SelectedInstanceId = instanceId ?? string.Empty;
        CancelPendingDetailsQuery();
        if (mDetailsQuery == null || string.IsNullOrWhiteSpace(SelectedInstanceId))
        {
            return Task.CompletedTask;
        }

        CancellationTokenSource cancellation = new();
        mDetailsQueryCancellation = cancellation;
        var queryVersion = Interlocked.Increment(ref mQueryVersion);
        return QueryInstanceCoreAsync(
            SelectedInstanceId,
            queryVersion,
            EngineId,
            SessionId,
            Generation,
            cancellation);
    }

    /// <summary>取消仍在等待 terminal response 的详情查询；页面关闭后不保留后台工作。</summary>
    public void Dispose()
    {
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        Interlocked.Increment(ref mQueryVersion);
        CancelPendingDetailsQuery();
    }

    /// <summary>执行单代详情查询，并只允许仍属于当前宿主和选择的结果提交。</summary>
    private async Task QueryInstanceCoreAsync(
        string instanceId,
        int queryVersion,
        string queryEngineId,
        string querySessionId,
        long queryGeneration,
        CancellationTokenSource cancellation)
    {
        try
        {
            var state = await mDetailsQuery!(instanceId, cancellation.Token);
            if (CanCommitDetailsQuery(
                state, instanceId, queryVersion, queryEngineId, querySessionId, queryGeneration))
            {
                mSelectedDetailsState = state;
                ApplyState(state);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 选择、宿主或窗口生命周期变化时取消属于正常控制流，不向用户显示失败。
        }
        catch (Exception exception)
        {
            if (queryVersion == Volatile.Read(ref mQueryVersion))
            {
                DiagnosticText = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.DetailQueryFailed") + exception.Message;
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref mDetailsQueryCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    /// <summary>集中校验详情查询版本、宿主、实例、时间和 Telemetry 所有权。</summary>
    private bool CanCommitDetailsQuery(
        WorkbenchFsmKitState state,
        string instanceId,
        int queryVersion,
        string queryEngineId,
        string querySessionId,
        long queryGeneration)
    {
        return queryVersion == Volatile.Read(ref mQueryVersion)
            && MatchesQueryHost(state, queryEngineId, querySessionId, queryGeneration)
            && IsExpectedDetailsState(state, instanceId)
            && IsDetailsStateCurrent(state, instanceId)
            && !HasSequencedTelemetryDetails(instanceId);
    }

    /// <summary>取消当前查询但由查询自身 finally 释放取消源，避免与运行中操作争用 Dispose。</summary>
    private void CancelPendingDetailsQuery()
    {
        var cancellation = Interlocked.Exchange(ref mDetailsQueryCancellation, null);
        cancellation?.Cancel();
    }

    /// <summary>语言切换时刷新 FsmKit 空状态、计数和可见诊断文本。</summary>
    private void OnCultureChanged()
    {
        bool isNotSelected = string.Equals(EngineId, "未选择", StringComparison.Ordinal)
            || string.Equals(EngineId, "Not selected", StringComparison.Ordinal);
        bool isUnknown = string.Equals(SessionId, "未知", StringComparison.Ordinal)
            || string.Equals(SessionId, "Unknown", StringComparison.Ordinal);
        bool isWaiting = string.Equals(Source, "等待数据", StringComparison.Ordinal)
            || string.Equals(Source, "Waiting for data", StringComparison.Ordinal);
        if (isNotSelected)
        {
            EngineId = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.NotSelected");
            SelectedMachineName = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.NotSelected");
            CurrentState = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.NotSelected");
        }

        if (isUnknown)
        {
            SessionId = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.Unknown");
            Mode = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.Unknown");
            UpdatedAtText = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.Unknown");
        }

        if (isWaiting)
        {
            Source = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.WaitingData");
            DataChannelText = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.WaitingData");
            DiagnosticText = WorkbenchI18nService.Instance.GetString("String.FsmKit.Status.WaitingState");
        }

        OnPropertyChanged(nameof(DiagnosticText));
        OnPropertyChanged(nameof(GraphEmptyHint));
        OnPropertyChanged(nameof(InstanceCountText));
        OnPropertyChanged(nameof(StateCountText));
        OnPropertyChanged(nameof(HistoryCountText));
    }
}
