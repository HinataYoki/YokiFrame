using YokiFrame.Tooling.Application.Models.PoolKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;

/// <summary>包装一个对象池 read model，并在周期帧之间保持列表项身份稳定。</summary>
public sealed class PoolKitPoolListItemViewModel : ViewModelBase
{
    private readonly Func<WorkbenchPoolKitObject, Task>? mOpenLocationAsync;
    private WorkbenchPoolKitPool mPool;
    private IReadOnlyList<PoolKitObjectListItemViewModel> mActiveObjects = Array.Empty<PoolKitObjectListItemViewModel>();
    private string mRecentEventText = string.Empty;
    private bool mIsLeakCandidate;

    /// <summary>创建绑定指定对象池的稳定列表项。</summary>
    /// <param name="pool">首帧对象池状态。</param>
    /// <param name="openLocationAsync">通过宿主打开借出位置的可选回调。</param>
    public PoolKitPoolListItemViewModel(
        WorkbenchPoolKitPool pool,
        Func<WorkbenchPoolKitObject, Task>? openLocationAsync)
    {
        mPool = pool;
        mOpenLocationAsync = openLocationAsync;
        mActiveObjects = CreateActiveObjects(pool.ActiveObjects);
    }

    /// <summary>获取帧内稳定对象池身份。</summary>
    public string Identity => mPool.Identity;
    /// <summary>获取 Runtime 会话内稳定对象池标识。</summary>
    public string PoolId => mPool.StablePoolId;
    /// <summary>获取当前关联的强类型对象池状态。</summary>
    internal WorkbenchPoolKitPool Pool => mPool;
    /// <summary>获取对象池名称。</summary>
    public string Name => mPool.Name;
    /// <summary>获取对象池完整类型名。</summary>
    public string TypeName => mPool.TypeName;
    /// <summary>获取当前借出数量。</summary>
    public int ActiveCount => mPool.ActiveCount;
    /// <summary>获取当前池内数量。</summary>
    public int InactiveCount => mPool.InactiveCount;
    /// <summary>获取当前总量。</summary>
    public int TotalCount => mPool.TotalCount;
    /// <summary>获取历史峰值。</summary>
    public int PeakCount => mPool.PeakCount;
    /// <summary>获取缓存上限展示文本。</summary>
    public string MaxCacheCountText => mPool.MaxCacheCountText;
    /// <summary>获取健康状态。</summary>
    public string HealthStatus => mPool.HealthStatus;
    /// <summary>获取当前池是否仍有借出对象，只作为疑似未归还提示。</summary>
    public bool HasActiveObjects => ActiveCount > 0;
    /// <summary>获取当前池是否进入最近一次显式检查的疑似未归还候选。</summary>
    public bool IsLeakCandidate => mIsLeakCandidate;
    /// <summary>获取当前池是否达到高压力阈值。</summary>
    public bool IsHighPressure => UsagePercent >= 90d;
    /// <summary>获取 0-100 的使用率。</summary>
    public double UsagePercent => Math.Clamp(mPool.UsageRate * 100d, 0d, 100d);
    /// <summary>获取使用率文本。</summary>
    public string UsagePercentText => UsagePercent.ToString("F0") + "%";
    /// <summary>获取借出对象总量。</summary>
    public int ActiveObjectTotal => mPool.ActiveObjectTotal;
    /// <summary>获取池内对象总量。</summary>
    public int InactiveObjectTotal => mPool.InactiveObjectTotal;
    /// <summary>获取借出对象是否被裁剪。</summary>
    public bool ActiveObjectTruncated => mPool.ActiveObjectTruncated;
    /// <summary>获取池内对象是否被裁剪。</summary>
    public bool InactiveObjectTruncated => mPool.InactiveObjectTruncated;
    /// <summary>获取借出对象明细。</summary>
    public IReadOnlyList<PoolKitObjectListItemViewModel> ActiveObjects => mActiveObjects;
    /// <summary>获取池内对象明细。</summary>
    public IReadOnlyList<WorkbenchPoolKitObject> InactiveObjects => mPool.InactiveObjects;
    /// <summary>获取当前池最近事件提示。</summary>
    public string RecentEventText
    {
        get => mRecentEventText;
        private set => SetProperty(ref mRecentEventText, value);
    }
    /// <summary>获取是否存在最近事件提示。</summary>
    public bool HasRecentEvent => !string.IsNullOrWhiteSpace(RecentEventText);

    /// <summary>应用同身份新帧并通知全部绑定指标。</summary>
    internal void Update(WorkbenchPoolKitPool pool, string recentEventText, bool isLeakCandidate)
    {
        mPool = pool;
        mActiveObjects = CreateActiveObjects(pool.ActiveObjects);
        mIsLeakCandidate = isLeakCandidate;
        RecentEventText = recentEventText;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(InactiveCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(PeakCount));
        OnPropertyChanged(nameof(MaxCacheCountText));
        OnPropertyChanged(nameof(HealthStatus));
        OnPropertyChanged(nameof(HasActiveObjects));
        OnPropertyChanged(nameof(IsLeakCandidate));
        OnPropertyChanged(nameof(IsHighPressure));
        OnPropertyChanged(nameof(UsagePercent));
        OnPropertyChanged(nameof(UsagePercentText));
        OnPropertyChanged(nameof(ActiveObjectTotal));
        OnPropertyChanged(nameof(InactiveObjectTotal));
        OnPropertyChanged(nameof(ActiveObjectTruncated));
        OnPropertyChanged(nameof(InactiveObjectTruncated));
        OnPropertyChanged(nameof(ActiveObjects));
        OnPropertyChanged(nameof(InactiveObjects));
        OnPropertyChanged(nameof(HasRecentEvent));
    }

    /// <summary>把有界借出对象包装为带源码定位命令的只读行。</summary>
    /// <param name="source">Runtime 状态中的有界借出对象。</param>
    /// <returns>与当前帧对应的可绑定对象行。</returns>
    private IReadOnlyList<PoolKitObjectListItemViewModel> CreateActiveObjects(
        IReadOnlyList<WorkbenchPoolKitObject> source)
    {
        PoolKitObjectListItemViewModel[] result = new PoolKitObjectListItemViewModel[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            result[index] = new PoolKitObjectListItemViewModel(source[index], mOpenLocationAsync);
        }

        return result;
    }

    /// <summary>判断搜索文本是否匹配名称、类型、健康或统计。</summary>
    internal bool Matches(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return true;
        return Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || TypeName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || HealthStatus.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || ActiveCount.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || TotalCount.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}
