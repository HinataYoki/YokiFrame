namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 统一 Kit 页面的忙碌、状态、错误和生命周期取消契约，不约束页面布局。
/// </summary>
public abstract class KitPageViewModel : ViewModelBase
{
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private bool mIsBusy;
    private bool mIsDisposed;
    private string mStatusText = string.Empty;
    private string mErrorText = string.Empty;

    /// <summary>创建默认可刷新的 Kit 页面。</summary>
    protected KitPageViewModel()
    {
    }

    /// <summary>创建指定初始可见性的 Kit 页面。</summary>
    /// <param name="isPageActive">初始是否允许周期刷新。</param>
    protected KitPageViewModel(bool isPageActive)
        : base(isPageActive)
    {
    }

    /// <summary>获取页面生命周期取消标记；释放后所有仍在运行的页面操作都应停止。</summary>
    protected CancellationToken LifetimeCancellationToken => mLifetimeCancellation.Token;

    /// <summary>获取页面是否已经释放；派生命令在启动 IO 前必须检查。</summary>
    protected bool IsDisposed => mIsDisposed;

    /// <summary>获取页面是否正在执行会阻塞重复提交的操作。</summary>
    public bool IsBusy
    {
        get => mIsBusy;
        protected set
        {
            if (SetProperty(ref mIsBusy, value))
            {
                OnIsBusyChanged(value);
            }
        }
    }

    /// <summary>获取最近一次面向用户的短状态。</summary>
    public string StatusText
    {
        get => mStatusText;
        protected set
        {
            if (SetProperty(ref mStatusText, value))
            {
                OnStatusTextChanged(value);
            }
        }
    }

    /// <summary>获取最近一次需要用户处理的错误文本；空文本表示没有错误。</summary>
    public string ErrorText
    {
        get => mErrorText;
        protected set
        {
            if (SetProperty(ref mErrorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>获取当前是否存在需要用户处理的错误。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    /// <summary>忙碌状态变化后的扩展点；派生页在这里刷新受忙碌状态影响的命令。</summary>
    /// <param name="isBusy">变化后的忙碌状态。</param>
    protected virtual void OnIsBusyChanged(bool isBusy)
    {
    }

    /// <summary>状态文本变化后的扩展点；派生页只在这里刷新由状态组成的摘要。</summary>
    /// <param name="statusText">变化后的状态文本。</param>
    protected virtual void OnStatusTextChanged(string statusText)
    {
    }

    /// <summary>进入忙碌状态并清除旧错误，供异步命令在启动前统一调用。</summary>
    /// <param name="statusText">可选的进行中状态；为空时保留当前状态文本。</param>
    protected void BeginOperation(string? statusText = null)
    {
        ErrorText = string.Empty;
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            StatusText = statusText;
        }

        IsBusy = true;
    }

    /// <summary>写入成功状态并结束忙碌；调用方负责确保结果仍属于当前操作。</summary>
    /// <param name="statusText">成功后的短状态。</param>
    protected void CompleteOperation(string statusText)
    {
        StatusText = statusText;
        IsBusy = false;
    }

    /// <summary>把异常映射成用户可见错误，并结束忙碌状态。</summary>
    /// <param name="exception">页面操作捕获的异常。</param>
    /// <param name="statusText">错误短状态。</param>
    protected void FailOperation(Exception exception, string statusText)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ErrorText = exception.Message;
        StatusText = statusText;
        IsBusy = false;
    }

    /// <summary>取消当前生命周期操作并释放取消源；重复调用保持幂等。</summary>
    protected void DisposePageResources()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        mLifetimeCancellation.Cancel();
        OnDisposing();
        mLifetimeCancellation.Dispose();
    }

    /// <summary>派生页释放外部订阅的扩展点；此时生命周期取消已经发出。</summary>
    protected virtual void OnDisposing()
    {
    }
}
