using System.Collections.ObjectModel;
using Avalonia.Media;
using YokiFrame.Tooling.Application.Models.SpatialKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 SpatialKit 索引列表、密度热力图和健康摘要。</summary>
public sealed class SpatialKitPageViewModel : ViewModelBase
{
    private const int EMPTY_RESOLUTION = 8;
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private long mGeneration;
    private long mVersion;
    private string mSource = "等待数据";
    private string mStaleReason = string.Empty;
    private int mActiveIndexCount;
    private int mEntityCount;
    private int mPartitionCount;
    private string mSelectedDiagnosticsId = string.Empty;
    private WorkbenchSpatialIndex? mSelectedIndex;
    private int mDensityRows = EMPTY_RESOLUTION;
    private int mDensityColumns = EMPTY_RESOLUTION;
    private string mDensitySummaryText = "等待密度数据";
    private string mDensityResolutionText = "--";
    private string mDensityOccupancyText = "--";
    private int mDensityMeanCount;
    private int mDensityP95Count;
    private int mDensityMaxCount;
    private string mHealthText = "等待数据";
    private bool mHasDensity;
    private bool mHasHealthWarning;

    /// <summary>获取当前 SpatialKit 索引实例。</summary>
    public ObservableCollection<WorkbenchSpatialIndex> Indexes { get; } = new();

    /// <summary>获取选中索引的密度 bin。</summary>
    public ObservableCollection<SpatialDensityCellViewModel> DensityCells { get; } = new();

    /// <summary>获取或设置当前选中的索引。</summary>
    public WorkbenchSpatialIndex? SelectedIndex
    {
        get => mSelectedIndex;
        set
        {
            if (SetProperty(ref mSelectedIndex, value))
            {
                mSelectedDiagnosticsId = value?.DiagnosticsId ?? string.Empty;
                OnPropertyChanged(nameof(SelectedDiagnosticsId));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ShowNoSelection));
                ApplyDensity(value?.Density);
            }
        }
    }

    /// <summary>获取当前数据源。</summary>
    public string Source { get => mSource; private set => SetProperty(ref mSource, value); }

    /// <summary>获取宿主 stale 诊断。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }

    /// <summary>获取活跃索引数量。</summary>
    public int ActiveIndexCount { get => mActiveIndexCount; private set => SetProperty(ref mActiveIndexCount, value); }

    /// <summary>获取实体总数。</summary>
    public int EntityCount { get => mEntityCount; private set => SetProperty(ref mEntityCount, value); }

    /// <summary>获取分区总数。</summary>
    public int PartitionCount { get => mPartitionCount; private set => SetProperty(ref mPartitionCount, value); }

    /// <summary>获取密度行数。</summary>
    public int DensityRows { get => mDensityRows; private set => SetProperty(ref mDensityRows, value); }

    /// <summary>获取密度列数。</summary>
    public int DensityColumns { get => mDensityColumns; private set => SetProperty(ref mDensityColumns, value); }

    /// <summary>获取密度统计摘要。</summary>
    public string DensitySummaryText { get => mDensitySummaryText; private set => SetProperty(ref mDensitySummaryText, value); }

    /// <summary>获取当前密度网格分辨率。</summary>
    public string DensityResolutionText { get => mDensityResolutionText; private set => SetProperty(ref mDensityResolutionText, value); }

    /// <summary>获取已占用 bin 与总 bin 的紧凑文本。</summary>
    public string DensityOccupancyText { get => mDensityOccupancyText; private set => SetProperty(ref mDensityOccupancyText, value); }

    /// <summary>获取每个 bin 的平均实体数。</summary>
    public int DensityMeanCount { get => mDensityMeanCount; private set => SetProperty(ref mDensityMeanCount, value); }

    /// <summary>获取密度分布的 P95 实体数。</summary>
    public int DensityP95Count { get => mDensityP95Count; private set => SetProperty(ref mDensityP95Count, value); }

    /// <summary>获取最密集 bin 的实体数。</summary>
    public int DensityMaxCount { get => mDensityMaxCount; private set => SetProperty(ref mDensityMaxCount, value); }

    /// <summary>获取当前索引健康摘要。</summary>
    public string HealthText { get => mHealthText; private set => SetProperty(ref mHealthText, value); }

    /// <summary>获取选中索引是否具有可渲染的密度数据。</summary>
    public bool HasDensity { get => mHasDensity; private set => SetProperty(ref mHasDensity, value); }

    /// <summary>获取密度健康状态是否需要提示用户关注。</summary>
    public bool HasHealthWarning { get => mHasHealthWarning; private set => SetProperty(ref mHasHealthWarning, value); }

    /// <summary>获取密度状态是否可以使用正向视觉呈现。</summary>
    public bool IsHealthPositive => HasDensity && !HasHealthWarning;

    /// <summary>获取当前索引数量文本。</summary>
    public string IndexCountText => Indexes.Count + " / " + ActiveIndexCount;

    /// <summary>获取当前是否存在可显示索引。</summary>
    public bool IsEmpty => Indexes.Count == 0;

    /// <summary>获取当前是否存在运行中的索引。</summary>
    public bool HasIndexes => !IsEmpty;

    /// <summary>获取当前是否存在选中索引。</summary>
    public bool HasSelection => SelectedIndex != null;

    /// <summary>获取有索引但尚未选中实例的占位状态。</summary>
    public bool ShowNoSelection => HasIndexes && !HasSelection;

    /// <summary>获取选中实例但没有密度数据的占位状态。</summary>
    public bool ShowDensityEmpty => HasSelection && !HasDensity;

    /// <summary>获取当前是否存在 stale 诊断。</summary>
    public bool HasStaleReason => !string.IsNullOrWhiteSpace(StaleReason);

    /// <summary>获取当前选中索引的诊断编号。</summary>
    public string SelectedDiagnosticsId => mSelectedDiagnosticsId;

    /// <summary>获取当前宿主 session。</summary>
    public string SessionId => mSessionId;

    /// <summary>获取当前宿主 generation。</summary>
    public long Generation => mGeneration;

    /// <summary>应用 dashboard 周期状态并拒绝旧宿主版本。</summary>
    public void ApplyPeriodicState(WorkbenchSpatialKitState? state)
    {
        if (state == null)
        {
            ResetState();
            return;
        }

        if (MatchesIdentity(state) && state.Version < mVersion)
        {
            StaleReason = state.StaleReason;
            NotifyStalePresentationChanged();
            return;
        }

        mEngineId = state.EngineId;
        mSessionId = state.SessionId;
        mGeneration = state.Generation;
        OnPropertyChanged(nameof(SessionId));
        OnPropertyChanged(nameof(Generation));
        mVersion = state.Version;
        Source = string.IsNullOrWhiteSpace(state.Source) ? "snapshot" : state.Source;
        StaleReason = state.StaleReason;
        ActiveIndexCount = state.ActiveIndexCount;
        EntityCount = state.EntityCount;
        PartitionCount = state.PartitionCount;
        ReplaceIndexes(state.Indexes);
        NotifyStalePresentationChanged();
        OnPropertyChanged(nameof(IndexCountText));
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>判断状态是否属于当前宿主会话。</summary>
    private bool MatchesIdentity(WorkbenchSpatialKitState state)
    {
        return string.Equals(mEngineId, state.EngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, state.SessionId, StringComparison.Ordinal)
            && mGeneration == state.Generation;
    }

    /// <summary>替换实例列表并尽量保留用户当前选择。</summary>
    private void ReplaceIndexes(IReadOnlyList<WorkbenchSpatialIndex> indexes)
    {
        WorkbenchSpatialIndex? selected = indexes.FirstOrDefault(
            item => string.Equals(item.DiagnosticsId, mSelectedDiagnosticsId, StringComparison.Ordinal));
        if (selected == null && indexes.Count > 0)
        {
            selected = indexes[0];
        }

        Indexes.Clear();
        for (int index = 0; index < indexes.Count; index++)
        {
            Indexes.Add(indexes[index]);
        }

        SelectedIndex = selected;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasIndexes));
        OnPropertyChanged(nameof(ShowNoSelection));
    }

    /// <summary>把选中索引密度投影成可绑定热力图单元格。</summary>
    private void ApplyDensity(WorkbenchSpatialDensity? density)
    {
        DensityCells.Clear();
        if (density == null || density.Resolution <= 0 || density.Bins.Count == 0)
        {
            DensityRows = EMPTY_RESOLUTION;
            DensityColumns = EMPTY_RESOLUTION;
            DensitySummaryText = "暂无密度数据";
            DensityResolutionText = "--";
            DensityOccupancyText = "--";
            DensityMeanCount = 0;
            DensityP95Count = 0;
            DensityMaxCount = 0;
            HealthText = "当前索引暂无密度数据";
            HasDensity = false;
            HasHealthWarning = false;
            NotifyDensityPresentationChanged();
            return;
        }

        DensityRows = density.Resolution;
        DensityColumns = density.Resolution;
        DensityResolutionText = density.Resolution + " x " + density.Resolution;
        DensityOccupancyText = density.OccupiedBins + " / " + density.TotalBins;
        DensityMeanCount = density.MeanCount;
        DensityP95Count = density.P95Count;
        DensityMaxCount = density.MaxCount;
        int maxCount = Math.Max(1, density.MaxCount);
        for (int index = 0; index < density.Bins.Count; index++)
        {
            int x = index % density.Resolution;
            int y = index / density.Resolution;
            int count = density.Bins[index];
            DensityCells.Add(new SpatialDensityCellViewModel(
                x,
                y,
                count,
                maxCount,
                "bin (" + x + ", " + y + ") · " + count + " entities"));
        }

        DensitySummaryText = density.OccupiedBins + "/" + density.TotalBins
            + " occupied · mean " + density.MeanCount
            + " · p95 " + density.P95Count
            + " · max " + density.MaxCount;
        HealthText = CreateHealthText(density);
        HasDensity = true;
        HasHealthWarning = RequiresHealthAttention(density);
        NotifyDensityPresentationChanged();
    }

    /// <summary>根据密度统计生成不改变索引参数的健康提示。</summary>
    private static string CreateHealthText(WorkbenchSpatialDensity density)
    {
        if (density.MaxCount == 0)
        {
            return "当前没有实体";
        }

        if (HasDensityHotspot(density))
        {
            return "存在明显热点分区，建议检查 cell size 或实体分布";
        }

        if (density.OccupiedBins * 4 < density.TotalBins)
        {
            return "分布较稀疏，当前索引仍可正常观察";
        }

        return "分布均衡，未发现明显热点";
    }

    /// <summary>判断密度是否存在显著热点，供健康文案和视觉状态复用同一规则。</summary>
    private static bool HasDensityHotspot(WorkbenchSpatialDensity density)
    {
        bool hasSparseHotspot = density.OccupiedBins > 0
            && density.OccupiedBins * 4 <= density.TotalBins
            && density.MaxCount > Math.Max(1, density.MeanCount) * 2;
        return (density.P95Count > 0 && density.MaxCount >= density.P95Count * 4)
            || hasSparseHotspot;
    }

    /// <summary>判断密度健康状态是否需要以警告色呈现。</summary>
    private static bool RequiresHealthAttention(WorkbenchSpatialDensity density)
    {
        return density.MaxCount > 0
            && (HasDensityHotspot(density) || density.OccupiedBins * 4 < density.TotalBins);
    }

    /// <summary>通知依赖密度可用性和健康色调的组合属性。</summary>
    private void NotifyDensityPresentationChanged()
    {
        OnPropertyChanged(nameof(IsHealthPositive));
        OnPropertyChanged(nameof(ShowDensityEmpty));
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>通知 stale 状态影响的组合属性，保持状态带和详情同步。</summary>
    private void NotifyStalePresentationChanged()
    {
        OnPropertyChanged(nameof(HasStaleReason));
    }

    /// <summary>清空当前运行状态并保留页面结构。</summary>
    private void ResetState()
    {
        mEngineId = string.Empty;
        mSessionId = string.Empty;
        mGeneration = 0L;
        mVersion = 0L;
        Source = "等待数据";
        StaleReason = string.Empty;
        ActiveIndexCount = 0;
        EntityCount = 0;
        PartitionCount = 0;
        Indexes.Clear();
        SelectedIndex = null;
        DensityCells.Clear();
        DensityRows = EMPTY_RESOLUTION;
        DensityColumns = EMPTY_RESOLUTION;
        DensitySummaryText = "等待密度数据";
        DensityResolutionText = "--";
        DensityOccupancyText = "--";
        DensityMeanCount = 0;
        DensityP95Count = 0;
        DensityMaxCount = 0;
        HealthText = "等待数据";
        HasDensity = false;
        HasHealthWarning = false;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasIndexes));
        OnPropertyChanged(nameof(HasStaleReason));
        OnPropertyChanged(nameof(IndexCountText));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowNoSelection));
        OnPropertyChanged(nameof(ShowDensityEmpty));
        OnPropertyChanged(nameof(IsHealthPositive));
    }
}

/// <summary>描述一个 Workbench 密度热力图单元格。</summary>
public sealed class SpatialDensityCellViewModel
{
    /// <summary>创建密度单元格并计算固定调色板颜色。</summary>
    public SpatialDensityCellViewModel(int x, int y, int count, int maxCount, string tooltipText)
    {
        X = x;
        Y = y;
        Count = count;
        TooltipText = tooltipText;
        Brush = CreateBrush(count, maxCount);
    }

    /// <summary>获取 bin 横坐标。</summary>
    public int X { get; }

    /// <summary>获取 bin 纵坐标。</summary>
    public int Y { get; }

    /// <summary>获取实体数量。</summary>
    public int Count { get; }

    /// <summary>获取单元格悬停文本。</summary>
    public string TooltipText { get; }

    /// <summary>获取单元格背景色。</summary>
    public IBrush Brush { get; }

    /// <summary>按占用比例生成稳定的冷暖两段调色板。</summary>
    private static IBrush CreateBrush(int count, int maxCount)
    {
        if (count <= 0)
        {
            return new SolidColorBrush(Color.FromRgb(31, 43, 55));
        }

        double ratio = Math.Max(0d, Math.Min(1d, count / (double)Math.Max(1, maxCount)));
        byte red = (byte)(45 + (int)(ratio * 190d));
        byte green = (byte)(96 + (int)((1d - ratio) * 58d));
        byte blue = (byte)(132 - (int)(ratio * 78d));
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}
