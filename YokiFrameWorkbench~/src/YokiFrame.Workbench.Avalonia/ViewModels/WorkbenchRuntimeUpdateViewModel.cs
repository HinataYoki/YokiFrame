using System.Diagnostics;
using System.Windows.Input;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 承载 Workbench 后台新版检测、显式重新编译和窗口生命周期取消状态。
/// </summary>
public sealed class WorkbenchRuntimeUpdateViewModel : ViewModelBase, IDisposable
{
    private readonly string mSourcePackageRoot;
    private readonly string mProjectRoot;
    private readonly string mRunningFingerprint;
    private readonly IWorkbenchRuntimeUpdateService mService;
    private readonly CancellationTokenSource mLifetime = new();
    private readonly CancellationToken mLifetimeToken;
    private readonly AsyncRelayCommand mRebuildCommand;
    private string mStatusText = string.Empty;
    private bool mIsBuilding;
    private bool mIsUpdateAvailable;
    private int mCheckStarted;
    private bool mDisposed;

    /// <summary>
    /// 创建并捕获 Workbench 启动时指针，确保后续外部发布不会改写本进程的版本基线。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="projectRoot">当前宿主项目根。</param>
    public WorkbenchRuntimeUpdateViewModel(string sourcePackageRoot, string projectRoot)
        : this(sourcePackageRoot, projectRoot, new WorkbenchRuntimeUpdateService())
    {
    }

    /// <summary>
    /// 使用可替换服务创建更新状态，供测试观察生命周期。
    /// </summary>
    internal WorkbenchRuntimeUpdateViewModel(
        string sourcePackageRoot,
        string projectRoot,
        IWorkbenchRuntimeUpdateService service)
    {
        mSourcePackageRoot = sourcePackageRoot;
        mProjectRoot = projectRoot;
        mService = service;
        mLifetimeToken = mLifetime.Token;
        mRunningFingerprint = WorkbenchRuntimeUpdateService.ReadCurrentFingerprint(projectRoot);
        mRebuildCommand = new AsyncRelayCommand(RebuildAsync, CanRebuild);
    }

    /// <summary>获取显式构建新版 Runtime 的命令。</summary>
    public ICommand RebuildCommand => mRebuildCommand;

    /// <summary>获取是否显示新版构建入口。</summary>
    public bool IsVisible => mIsUpdateAvailable || mIsBuilding;

    /// <summary>获取是否显示更新结果文本。</summary>
    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(mStatusText);

    /// <summary>获取当前构建按钮文本。</summary>
    public string ButtonText => mIsBuilding ? "正在编译..." : "重新编译新版";

    /// <summary>获取新版检测或构建结果。</summary>
    public string StatusText
    {
        get => mStatusText;
        private set
        {
            if (SetProperty(ref mStatusText, value))
            {
                OnPropertyChanged(nameof(IsStatusVisible));
            }
        }
    }

    /// <summary>
    /// 在窗口打开后启动唯一后台检测；重复调用复用已开始状态。
    /// </summary>
    /// <returns>检测进入终态后完成。</returns>
    public Task StartCheckAsync()
    {
        return !mDisposed && Interlocked.CompareExchange(ref mCheckStarted, 1, 0) == 0
            ? CheckForUpdateAsync()
            : Task.CompletedTask;
    }

    /// <summary>
    /// 取消源码扫描或正在运行的 dotnet 构建，防止窗口关闭后继续回写 UI。
    /// </summary>
    public void Dispose()
    {
        if (mDisposed)
        {
            return;
        }

        mDisposed = true;
        mLifetime.Cancel();
        mLifetime.Dispose();
    }

    /// <summary>
    /// 计算当前源码指纹并仅在窗口仍存活时发布可用更新状态。
    /// </summary>
    private async Task CheckForUpdateAsync()
    {
        try
        {
            var result = await mService.CheckAsync(
                mSourcePackageRoot,
                mProjectRoot,
                mRunningFingerprint,
                mLifetimeToken);
            if (mDisposed)
            {
                return;
            }

            SetUpdateAvailable(result.UpdateAvailable);
        }
        catch (OperationCanceledException) when (mLifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!mDisposed)
            {
                Trace.TraceError("Workbench Runtime update check failed: {0}", exception);
                StatusText = "新版检测失败";
            }
        }
    }

    /// <summary>
    /// 执行用户显式触发的 Runtime 重新编译，成功后提示重新打开 Workbench 生效。
    /// </summary>
    private async Task RebuildAsync()
    {
        SetBuilding(true);
        StatusText = string.Empty;
        try
        {
            await mService.RebuildAsync(mSourcePackageRoot, mProjectRoot, mLifetimeToken);
            if (!mDisposed)
            {
                SetUpdateAvailable(false);
                StatusText = "新版已编译，重新打开后生效";
            }
        }
        catch (OperationCanceledException) when (mLifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!mDisposed)
            {
                Trace.TraceError("Workbench Runtime rebuild failed: {0}", exception);
                StatusText = "新版编译失败";
            }
        }
        finally
        {
            if (!mDisposed)
            {
                SetBuilding(false);
            }
        }
    }

    /// <summary>判断当前是否允许用户重新编译新版。</summary>
    private bool CanRebuild()
    {
        return !mDisposed && mIsUpdateAvailable;
    }

    /// <summary>更新可用新版状态并刷新按钮可见性与可执行性。</summary>
    private void SetUpdateAvailable(bool value)
    {
        if (mIsUpdateAvailable == value)
        {
            return;
        }

        mIsUpdateAvailable = value;
        OnPropertyChanged(nameof(IsVisible));
        mRebuildCommand.RaiseCanExecuteChanged();
    }

    /// <summary>更新构建中状态并刷新按钮文本与可见性。</summary>
    private void SetBuilding(bool value)
    {
        if (mIsBuilding == value)
        {
            return;
        }

        mIsBuilding = value;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(ButtonText));
    }
}
