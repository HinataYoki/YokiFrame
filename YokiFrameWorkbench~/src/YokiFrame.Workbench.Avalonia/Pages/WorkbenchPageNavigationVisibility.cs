namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 描述 Workbench 页面是否出现在用户可见的一级导航中。
/// </summary>
public enum WorkbenchPageNavigationVisibility
{
    /// <summary>
    /// 页面作为稳定产品入口显示在左侧导航。
    /// </summary>
    Primary,

    /// <summary>
    /// 页面保留内部路由和现有能力，但不占用一级导航。
    /// </summary>
    Hidden
}
