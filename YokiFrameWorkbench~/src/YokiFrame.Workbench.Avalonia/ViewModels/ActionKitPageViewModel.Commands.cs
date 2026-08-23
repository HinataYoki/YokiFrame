using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ActionKit 页面显式堆栈诊断命令。</summary>
public sealed partial class ActionKitPageViewModel
{
    /// <summary>切换后续根动作的 Start 堆栈捕获。</summary>
    private async Task ToggleStackTraceAsync()
    {
        if (mSetStackTraceAsync == null)
        {
            return;
        }

        HostIdentity requestIdentity = CaptureIdentity();
        try
        {
            WorkbenchActionKitState state = await mSetStackTraceAsync(
                requestIdentity.EngineId,
                !StackTraceEnabled,
                mLifetimeCancellation.Token);
            if (!TryApplyCommandState(requestIdentity, state)) return;
            OperationStatusText = WorkbenchI18nService.Instance.GetString(
                StackTraceEnabled ? "String.ActionKit.Status.StackEnabled" : "String.ActionKit.Status.StackDisabled");
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested)
        {
            OperationStatusText = string.Empty;
        }
        catch (Exception exception)
        {
            if (MatchesIdentity(requestIdentity))
                OperationStatusText = WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.StackSetFailed") + exception.Message;
        }
    }

    /// <summary>清空当前活动根的已捕获堆栈。</summary>
    private async Task ClearStackTraceAsync()
    {
        if (mClearStackTraceAsync == null)
        {
            return;
        }

        HostIdentity requestIdentity = CaptureIdentity();
        try
        {
            WorkbenchActionKitState state = await mClearStackTraceAsync(
                requestIdentity.EngineId,
                mLifetimeCancellation.Token);
            if (!TryApplyCommandState(requestIdentity, state)) return;
            OperationStatusText = WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.StackCleared");
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested)
        {
            OperationStatusText = string.Empty;
        }
        catch (Exception exception)
        {
            if (MatchesIdentity(requestIdentity))
                OperationStatusText = WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.StackClearFailed") + exception.Message;
        }
    }

    /// <summary>捕获异步命令发起时的完整宿主身份。</summary>
    /// <returns>不可变宿主身份快照。</returns>
    private HostIdentity CaptureIdentity() => new(mEngineId, mSessionId, mGeneration);

    /// <summary>仅应用仍属于当前宿主且未落后于页面版本的显式命令结果。</summary>
    /// <param name="requestIdentity">命令发起时的宿主身份。</param>
    /// <param name="state">Application 返回的强类型状态。</param>
    /// <returns>状态已安全应用时返回 true。</returns>
    private bool TryApplyCommandState(HostIdentity requestIdentity, WorkbenchActionKitState state)
    {
        if (!MatchesIdentity(requestIdentity)
            || !MatchesIdentity(state, requestIdentity)
            || state.Version < mVersion)
        {
            return false;
        }

        ApplyState(state);
        return true;
    }

    /// <summary>判断页面当前宿主是否仍等于请求身份。</summary>
    /// <param name="identity">待比较的请求身份。</param>
    /// <returns>engine、session 与 generation 全部匹配时返回 true。</returns>
    private bool MatchesIdentity(HostIdentity identity)
    {
        return string.Equals(mEngineId, identity.EngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, identity.SessionId, StringComparison.Ordinal)
            && mGeneration == identity.Generation;
    }

    /// <summary>判断返回状态是否属于命令发起时的宿主身份。</summary>
    /// <param name="state">Application 返回状态。</param>
    /// <param name="identity">命令发起身份。</param>
    /// <returns>完整身份匹配时返回 true。</returns>
    private static bool MatchesIdentity(WorkbenchActionKitState state, HostIdentity identity)
    {
        return string.Equals(state.EngineId, identity.EngineId, StringComparison.Ordinal)
            && string.Equals(state.SessionId, identity.SessionId, StringComparison.Ordinal)
            && state.Generation == identity.Generation;
    }

    /// <summary>判断当前页面能否切换堆栈设置。</summary>
    private bool CanSetStackTrace()
    {
        return mSetStackTraceAsync != null && !string.IsNullOrWhiteSpace(mEngineId);
    }

    /// <summary>判断当前页面能否清空已捕获堆栈。</summary>
    private bool CanClearStackTrace()
    {
        return mClearStackTraceAsync != null
            && !string.IsNullOrWhiteSpace(mEngineId)
            && StackTraceCount > 0;
    }

    /// <summary>通知两个异步命令重新计算可执行状态。</summary>
    private void RaiseOperationCommands()
    {
        ToggleStackTraceCommand.RaiseCanExecuteChanged();
        ClearStackTraceCommand.RaiseCanExecuteChanged();
    }

    /// <summary>保存异步命令发起时的宿主三元身份。</summary>
    private readonly record struct HostIdentity(string EngineId, string SessionId, long Generation);
}
