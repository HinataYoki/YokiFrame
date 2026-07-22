using System.Windows.Input;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 提供轻量级 Avalonia 命令封装，避免首屏工具为少量按钮引入额外 MVVM 依赖。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action mExecute;
    private readonly Func<bool>? mCanExecute;

    /// <summary>
    /// 创建命令实例。
    /// </summary>
    /// <param name="execute">命令执行委托。</param>
    /// <param name="canExecute">命令可执行判断；为空时始终可执行。</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        mExecute = execute;
        mCanExecute = canExecute;
    }

    /// <summary>
    /// 当命令可执行状态变化时通知 Avalonia 重新计算按钮启用状态。
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 判断当前命令是否允许执行。
    /// </summary>
    /// <param name="parameter">Avalonia 命令参数；当前命令不使用。</param>
    /// <returns>允许执行时返回 true。</returns>
    public bool CanExecute(object? parameter)
    {
        return mCanExecute?.Invoke() ?? true;
    }

    /// <summary>
    /// 执行命令委托，实际错误处理由注入委托负责。
    /// </summary>
    /// <param name="parameter">Avalonia 命令参数；当前命令不使用。</param>
    public void Execute(object? parameter)
    {
        mExecute();
    }

    /// <summary>
    /// 主动通知命令状态变化，供未来需要禁用按钮时复用。
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
