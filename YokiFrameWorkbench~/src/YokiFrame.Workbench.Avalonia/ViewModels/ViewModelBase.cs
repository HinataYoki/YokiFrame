using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 提供 Avalonia ViewModel 共享的属性变更通知和页面激活门控。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    private bool mIsPageActive = true;

    /// <summary>创建默认可刷新的页面；Shell 会在页面隐藏时显式关闭刷新。</summary>
    protected ViewModelBase()
        : this(true)
    {
    }

    /// <summary>创建指定初始可见性的页面，供设计时或无需门控的页面使用。</summary>
    /// <param name="isPageActive">初始是否允许周期刷新。</param>
    protected ViewModelBase(bool isPageActive)
    {
        mIsPageActive = isPageActive;
    }

    /// <summary>获取页面当前是否处于可见工作区；隐藏页只保留最后一次状态。</summary>
    protected bool IsPageActive => mIsPageActive;

    /// <summary>设置页面激活状态。Shell 切换页面时调用，隐藏页停止周期刷新。</summary>
    /// <param name="isActive">当前页面可见时为 true。</param>
    public void SetPageActive(bool isActive)
    {
        var changed = mIsPageActive != isActive;
        mIsPageActive = isActive;
        if (changed)
        {
            OnPageActiveChanged(isActive);
        }

        // 页面默认已激活；重复激活仍通知派生页，避免首次显式激活被去重后跳过加载。
        if (isActive)
        {
            OnPageActivated();
        }
    }

    /// <summary>页面激活状态变化后的扩展点；派生页只在这里处理文件读取等附加生命周期。</summary>
    /// <param name="isActive">变化后的激活状态。</param>
    protected virtual void OnPageActiveChanged(bool isActive)
    {
    }

    /// <summary>页面处于激活状态时的重复通知；派生页用于补做只执行一次的加载。</summary>
    protected virtual void OnPageActivated()
    {
    }

    /// <summary>
    /// 缓存隐藏页收到的最后一帧周期状态，并在页面重新激活时重放。
    /// </summary>
    /// <typeparam name="TState">页面周期状态类型。</typeparam>
    /// <param name="state">本轮状态；空值也需要缓存，以便激活后清空旧投影。</param>
    /// <param name="pendingState">页面隐藏期间保留的最后一帧。</param>
    /// <param name="apply">页面可见时执行的实际投影。</param>
    protected void CachePeriodicState<TState>(
        TState? state,
        ref TState? pendingState,
        Action<TState?> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (!IsPageActive)
        {
            pendingState = state;
            return;
        }

        pendingState = default;
        apply(state);
    }

    /// <summary>重放隐藏期间缓存的最后一帧；没有缓存时保持当前投影。</summary>
    /// <typeparam name="TState">页面周期状态类型。</typeparam>
    /// <param name="pendingState">页面隐藏期间保留的最后一帧。</param>
    /// <param name="apply">页面可见时执行的实际投影。</param>
    protected void ReplayPeriodicState<TState>(
        ref TState? pendingState,
        Action<TState?> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (!IsPageActive)
        {
            return;
        }

        TState? state = pendingState;
        pendingState = default;
        if (state != null)
        {
            apply(state);
        }
    }
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
