namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Workbench 左侧导航中的一个入口。
/// </summary>
public sealed class WorkbenchNavigationItem : ViewModelBase
{
    private bool mIsSelected;

    /// <summary>
    /// 创建导航入口。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <param name="displayName">页面显示名称。</param>
    /// <param name="iconKey">用于解析 Tauri 对齐矢量图标和语义色的稳定键。</param>
    public WorkbenchNavigationItem(string pageName, string displayName, string iconKey)
    {
        PageName = pageName;
        DisplayName = displayName;
        IconKey = iconKey;
    }

    /// <summary>
    /// 获取页面内部名称。
    /// </summary>
    public string PageName { get; }

    /// <summary>
    /// 获取页面显示名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 获取用于解析导航矢量图标的稳定键。
    /// </summary>
    public string IconKey { get; }

    /// <summary>
    /// 获取或设置当前入口是否被选中。
    /// </summary>
    public bool IsSelected
    {
        get => mIsSelected;
        set => SetProperty(ref mIsSelected, value);
    }
}
