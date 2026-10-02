using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 描述一个编译期 Workbench 页面及其导航元数据和状态投影函数。
/// </summary>
public sealed class WorkbenchPageModule
{
    private readonly Func<WorkbenchDashboardState, IReadOnlyList<WorkbenchDisplaySection>> mSectionFactory;

    /// <summary>
    /// 创建页面模块；Catalog 会统一校验页面名唯一性和默认页有效性。
    /// </summary>
    /// <param name="pageName">页面内部稳定名称。</param>
    /// <param name="displayName">导航和详情标题使用的显示名称。</param>
    /// <param name="groupName">左侧导航分组名称。</param>
    /// <param name="iconKey">当前导航样式使用的矢量图标键。</param>
    /// <param name="presentation">页面呈现类型。</param>
    /// <param name="navigationVisibility">页面是否进入用户可见的一级导航。</param>
    /// <param name="sectionFactory">把 dashboard 状态投影为详情段落的函数。</param>
    public WorkbenchPageModule(
        string pageName,
        string displayName,
        string groupName,
        string iconKey,
        WorkbenchPagePresentation presentation,
        WorkbenchPageNavigationVisibility navigationVisibility,
        Func<WorkbenchDashboardState, IReadOnlyList<WorkbenchDisplaySection>> sectionFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(sectionFactory);
        PageName = pageName;
        DisplayName = displayName;
        PageTitle = displayName;
        GroupName = groupName;
        IconKey = iconKey ?? string.Empty;
        Presentation = presentation;
        NavigationVisibility = navigationVisibility;
        mSectionFactory = sectionFactory;
        SupportedEngineKinds = Array.Empty<string>();
    }

    /// <summary>
    /// 获取页面内部稳定名称。
    /// </summary>
    public string PageName { get; }

    /// <summary>
    /// 获取页面显示名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 获取或初始化紧凑页头使用的页面标题；默认与导航显示名称一致。
    /// </summary>
    public string PageTitle { get; init; }

    /// <summary>
    /// 获取或初始化紧凑页头的一句话功能介绍。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 获取左侧导航分组名称。
    /// </summary>
    public string GroupName { get; }

    /// <summary>
    /// 获取导航矢量图标键。
    /// </summary>
    public string IconKey { get; }

    /// <summary>
    /// 获取页面呈现类型。
    /// </summary>
    public WorkbenchPagePresentation Presentation { get; }

    /// <summary>
    /// 获取页面是否进入用户可见的一级导航。
    /// </summary>
    public WorkbenchPageNavigationVisibility NavigationVisibility { get; }

    /// <summary>
    /// 获取页面支持的引擎种类。空列表表示 Unity 与 Godot 都显示。
    /// </summary>
    public IReadOnlyList<string> SupportedEngineKinds { get; init; }

    /// <summary>
    /// 判断当前 engine 是否应显示该页面。未选择 engine 时保留页面，避免连接前闪空导航。
    /// </summary>
    /// <param name="engineId">当前 engine 标识，例如 unity-editor 或 godot-runtime。</param>
    /// <returns>页面可在该 engine 下显示时返回 true。</returns>
    public bool SupportsEngine(string? engineId)
    {
        if (SupportedEngineKinds == null || SupportedEngineKinds.Count == 0 || string.IsNullOrWhiteSpace(engineId))
        {
            return true;
        }

        var kind = engineId.Contains("godot", StringComparison.OrdinalIgnoreCase)
            ? "Godot"
            : engineId.Contains("unity", StringComparison.OrdinalIgnoreCase)
                ? "Unity"
                : string.Empty;
        if (kind.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < SupportedEngineKinds.Count; index++)
        {
            if (string.Equals(SupportedEngineKinds[index], kind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 使用当前模块的投影函数创建详情段落。
    /// </summary>
    /// <param name="state">当前 dashboard 状态。</param>
    /// <returns>页面详情段落。</returns>
    public IReadOnlyList<WorkbenchDisplaySection> CreateSections(WorkbenchDashboardState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return mSectionFactory(state);
    }
}
