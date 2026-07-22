using System.Diagnostics;
using System.Windows.Input;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 执行单个异步 UI 操作，并在运行期间阻止重复提交。
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> mExecuteAsync;
    private readonly Func<bool>? mCanExecute;
    private int mIsRunning;

    /// <summary>
    /// 创建异步命令。
    /// </summary>
    /// <param name="executeAsync">需要执行的异步操作；调用方负责把预期错误转换为 UI 状态。</param>
    /// <param name="canExecute">额外启用条件；为空时仅受运行状态约束。</param>
    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        mExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        mCanExecute = canExecute;
    }

    /// <summary>
    /// 当运行状态或外部启用条件变化时通知 Avalonia 重新计算按钮状态。
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 判断命令当前是否允许执行。
    /// </summary>
    /// <param name="parameter">Avalonia 命令参数；当前命令不使用。</param>
    /// <returns>未运行且满足外部条件时返回 true。</returns>
    public bool CanExecute(object? parameter)
    {
        return Volatile.Read(ref mIsRunning) == 0 && (mCanExecute?.Invoke() ?? true);
    }

    /// <summary>
    /// 从 Avalonia 命令入口启动异步操作；ICommand 无法返回 Task，因此在此边界记录未处理异常。
    /// </summary>
    /// <param name="parameter">Avalonia 命令参数；当前命令不使用。</param>
    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception exception)
        {
            // ICommand.Execute 是 fire-and-forget 入口，不能把异常抛回 Avalonia 同步上下文。
            Trace.TraceError("Workbench async command execution failed: {0}", exception);
        }
    }

    /// <summary>
    /// 执行异步操作并返回可等待任务，供测试和组合流程观察完成状态。
    /// </summary>
    /// <returns>实际执行任务；命令不可执行或已在运行时返回已完成任务。</returns>
    public Task ExecuteAsync()
    {
        if ((mCanExecute?.Invoke() ?? true) == false
            || Interlocked.CompareExchange(ref mIsRunning, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        return ExecuteCoreAsync();
    }

    /// <summary>
    /// 在执行前后发布启用状态变化，并确保失败时也恢复可执行状态。
    /// </summary>
    /// <returns>注入异步操作的完成任务。</returns>
    private async Task ExecuteCoreAsync()
    {
        RaiseCanExecuteChanged();
        try
        {
            await mExecuteAsync();
        }
        finally
        {
            Interlocked.Exchange(ref mIsRunning, 0);
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 主动通知外部启用条件已变化。
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
