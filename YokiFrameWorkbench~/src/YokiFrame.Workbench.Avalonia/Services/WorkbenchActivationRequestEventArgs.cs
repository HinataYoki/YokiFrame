namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 表示另一个进程发来的窗口激活请求，并由当前窗口显式确认是否已接管。
/// </summary>
public sealed class WorkbenchActivationRequestEventArgs : EventArgs
{
    private int mAccepted;

    /// <summary>
    /// 获取当前窗口是否已确认接管激活请求。
    /// </summary>
    public bool IsAccepted => Volatile.Read(ref mAccepted) == 1;

    /// <summary>
    /// 在窗口仍可用且激活动作已成功排入 UI 线程后确认请求。
    /// </summary>
    public void Accept()
    {
        Interlocked.Exchange(ref mAccepted, 1);
    }
}
