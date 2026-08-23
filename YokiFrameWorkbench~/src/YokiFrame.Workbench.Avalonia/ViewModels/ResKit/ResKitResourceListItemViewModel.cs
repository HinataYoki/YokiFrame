using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels.ResKit;

/// <summary>包装一个 ResKit 资源摘要，并在周期帧之间保持列表项身份稳定。</summary>
public sealed class ResKitResourceListItemViewModel : ViewModelBase
{
    private WorkbenchResKitResource mResource;
    private string mDisplayName = string.Empty;
    private string mParentPath = string.Empty;
    private string mTypeDisplayName = string.Empty;

    /// <summary>创建绑定指定资源的稳定列表项。</summary>
    public ResKitResourceListItemViewModel(WorkbenchResKitResource resource)
    {
        mResource = resource;
        UpdateDisplayValues();
    }

    /// <summary>获取资源稳定身份。</summary>
    public string Identity => mResource.Identity;
    /// <summary>获取资源路径。</summary>
    public string Path => mResource.Path;
    /// <summary>获取适合高密度列表展示的资源名。</summary>
    public string DisplayName => mDisplayName;
    /// <summary>获取资源名之前的父路径。</summary>
    public string ParentPath => mParentPath;
    /// <summary>获取列表使用的短类型名。</summary>
    public string TypeDisplayName => mTypeDisplayName;
    /// <summary>获取完整类型名。</summary>
    public string TypeName => mResource.TypeName;
    /// <summary>获取加载状态。</summary>
    public string State => mResource.State;
    /// <summary>获取独立 lease 数量。</summary>
    public int LeaseCount => mResource.LeaseCount;
    /// <summary>获取实际 Provider 名称。</summary>
    public string ProviderName => mResource.ProviderName;
    /// <summary>获取 Provider 代次。</summary>
    public long ProviderGeneration => mResource.ProviderGeneration;
    /// <summary>获取已跟踪来源数量。</summary>
    public int TrackedSourceCount => mResource.TrackedSourceCount;
    /// <summary>获取周期状态携带的有界来源预览。</summary>
    internal IReadOnlyList<WorkbenchResKitLoadSource> SourcePreview => mResource.Sources;
    /// <summary>获取当前活动 lease 来源总数。</summary>
    internal int SourceTotal => mResource.SourceTotal;
    /// <summary>获取来源预览是否省略了其余 lease。</summary>
    internal bool SourcesTruncated => mResource.SourcesTruncated;
    /// <summary>获取资源当前是否持有 lease。</summary>
    public bool HasLeases => LeaseCount > 0;
    /// <summary>获取资源状态是否为就绪。</summary>
    public bool IsReady => string.Equals(State, "Ready", StringComparison.OrdinalIgnoreCase);
    /// <summary>获取屏幕阅读器使用的资源摘要。</summary>
    public string AutomationName => Path + "，" + TypeName + "，lease " + LeaseCount
        + WorkbenchI18nService.Instance.GetString("String.ResKit.AutomationStateSuffix", "，状态 ") + State;

    /// <summary>应用同身份新帧并通知全部绑定指标。</summary>
    internal void Update(WorkbenchResKitResource resource)
    {
        mResource = resource;
        UpdateDisplayValues();
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ParentPath));
        OnPropertyChanged(nameof(TypeDisplayName));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(LeaseCount));
        OnPropertyChanged(nameof(ProviderName));
        OnPropertyChanged(nameof(ProviderGeneration));
        OnPropertyChanged(nameof(TrackedSourceCount));
        OnPropertyChanged(nameof(HasLeases));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(AutomationName));
    }

    /// <summary>按当前语言刷新行的展示文本；行身份与 Runtime 数据不变。</summary>
    internal void RefreshLocalization()
    {
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(ParentPath));
    }

    /// <summary>在周期状态到达时预计算列表文本，避免 Avalonia 重复读取绑定属性时分配字符串。</summary>
    private void UpdateDisplayValues()
    {
        mDisplayName = GetDisplayName(mResource.Path);
        mParentPath = GetParentPath(mResource.Path);
        mTypeDisplayName = GetDisplayName(mResource.TypeName.Replace('.', '/'));
    }

    /// <summary>判断搜索文本是否匹配路径、类型、Provider、状态或引用数。</summary>
    internal bool Matches(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return true;
        return Path.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || TypeName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || ProviderName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || State.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || LeaseCount.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>从资源键或类型名中提取最后一个可读段。</summary>
    private static string GetDisplayName(string value)
    {
        int separatorIndex = GetLastPathSeparatorIndex(value);
        return separatorIndex < 0 || separatorIndex == value.Length - 1
            ? value
            : value[(separatorIndex + 1)..];
    }

    /// <summary>从资源键中保留父路径，根级资源显示统一占位。</summary>
    private static string GetParentPath(string value)
    {
        int separatorIndex = GetLastPathSeparatorIndex(value);
        return separatorIndex <= 0
            ? WorkbenchI18nService.Instance.GetString("String.ResKit.RootPath", "根路径")
            : value[..separatorIndex];
    }

    /// <summary>返回正斜杠或反斜杠中最后出现的位置，不创建临时字符数组。</summary>
    private static int GetLastPathSeparatorIndex(string value)
    {
        return Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
    }
}
