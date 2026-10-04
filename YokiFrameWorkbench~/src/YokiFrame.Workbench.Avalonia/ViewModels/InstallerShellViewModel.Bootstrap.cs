using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 Installer Godot Runtime 自举和 UI 线程回投。</summary>
public sealed partial class InstallerShellViewModel
{
    /// <summary>
    /// 在首次 Godot 计划缺少 Runtime 缓存时自动构建，并在同一 Installer 会话中重新规划。
    /// </summary>
    /// <param name="options">触发缓存门控的当前安装输入。</param>
    /// <param name="cancellationToken">输入被替代或窗口关闭时使用的令牌。</param>
    /// <returns>缓存构建和重新规划完成任务。</returns>
    private async Task BootstrapGodotRuntimeForPlanAsync(
        InstallerInstallOptions options,
        CancellationToken cancellationToken)
    {
        await mGodotRuntimeBootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!RequiresGodotRuntimeBootstrap(mSession.State, options))
            {
                return;
            }

            await RunGodotRuntimeBootstrapProcessAsync(
                openInstaller: false,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            PostToUi(() => AppendLocalLog(WorkbenchI18nService.Instance.GetString(
                "String.Installer.Log.RuntimeReadyReplanning",
                "Runtime 已构建完成，正在重新生成安装计划。")));
            await mSession.PrepareAsync(options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            mGodotRuntimeBootstrapGate.Release();
        }
    }

    /// <summary>
    /// 执行一次 Runtime bootstrap 子进程，并统一管理构建期间的页面状态。
    /// </summary>
    /// <param name="openInstaller">成功后是否启动新的 Installer。</param>
    /// <param name="cancellationToken">当前构建取消令牌。</param>
    /// <returns>子进程完成任务。</returns>
    private async Task RunGodotRuntimeBootstrapProcessAsync(
        bool openInstaller,
        CancellationToken cancellationToken)
    {
        BeginGodotRuntimeBootstrapPresentation(openInstaller);
        var succeeded = false;
        try
        {
            if (openInstaller)
            {
                await mGodotRuntimeBootstrapper.BootstrapAndOpenInstallerAsync(
                    SourcePackageRoot,
                    TargetProjectRoot,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await mGodotRuntimeBootstrapper.BootstrapAsync(
                    SourcePackageRoot,
                    TargetProjectRoot,
                    cancellationToken).ConfigureAwait(false);
            }

            succeeded = true;
        }
        finally
        {
            await EndGodotRuntimeBootstrapPresentationAsync(succeeded).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 发布 Runtime 构建开始状态；后台计划线程通过 UI 上下文更新 Avalonia 绑定。
    /// </summary>
    /// <param name="openInstaller">成功后是否会启动新的 Installer。</param>
    private void BeginGodotRuntimeBootstrapPresentation(bool openInstaller)
    {
        mIsGodotRuntimeBootstrapRunning = true;
        mIsGodotRuntimeBootstrapOpeningInstaller = openInstaller;
        var message = GetBootstrapStatusText(openInstaller);
        PostToUi(() =>
        {
            OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
            IsProgressVisible = true;
            ProgressValue = 0;
            SessionStatusText = message;
            ClearOutcomeDetails();
            AppendLocalLog(WorkbenchI18nService.Instance.GetString(
                "String.Installer.Log.BuildingGodotRuntime",
                "正在从选定 YokiFrame 源码包构建 Godot 项目 Runtime。"));
            RaiseCommandStates();
        });
    }

    /// <summary>
    /// 发布 Runtime 构建结束状态；成功时清除旧的前置失败，失败时恢复真实错误详情。
    /// </summary>
    /// <param name="succeeded">Runtime 构建是否成功。</param>
    private Task EndGodotRuntimeBootstrapPresentationAsync(bool succeeded)
    {
        mIsGodotRuntimeBootstrapRunning = false;
        mIsGodotRuntimeBootstrapOpeningInstaller = false;
        return PostToUiAndWaitAsync(() =>
        {
            OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
            IsProgressVisible = false;
            if (succeeded)
            {
                ClearOutcomeDetails();
            }
            else
            {
                ApplyOutcomeDetails(mSession.State);
            }

            RaiseCommandStates();
        });
    }

    /// <summary>
    /// 等待当前线程之前投递到 UI 上下文的状态投影完成，确保工作流任务返回时页面已达到同一终态。
    /// </summary>
    /// <param name="action">需要在 UI 上下文执行的状态更新。</param>
    /// <returns>状态更新执行完成任务。</returns>
    private Task PostToUiAndWaitAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (mSynchronizationContext == null
            || ReferenceEquals(SynchronizationContext.Current, mSynchronizationContext))
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mSynchronizationContext.Post(
            static state =>
            {
                var dispatch = (UiDispatch)state!;
                try
                {
                    dispatch.Action();
                    dispatch.Completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    dispatch.Completion.TrySetException(exception);
                }
            },
            new UiDispatch(action, completion));
        return completion.Task;
    }

    /// <summary>
    /// 将非 UI 计划线程的页面更新投递回创建 ViewModel 的上下文。
    /// </summary>
}
