using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 提供 Avalonia ViewModel 共享的属性变更通知能力。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// 当可绑定属性发生变化时通知 Avalonia 重新计算绑定。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 设置字段并在值确实变化时触发属性变更通知，避免重复刷新 UI。
    /// </summary>
    /// <param name="storage">属性背后的字段引用。</param>
    /// <param name="value">准备写入的新值。</param>
    /// <param name="propertyName">调用方属性名，默认由编译器填充。</param>
    /// <typeparam name="T">字段值类型。</typeparam>
    /// <returns>值发生变化时返回 true，否则返回 false。</returns>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 触发指定属性的变更通知，供派生类在批量状态更新后显式刷新绑定。
    /// </summary>
    /// <param name="propertyName">发生变化的属性名，默认由编译器填充。</param>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
