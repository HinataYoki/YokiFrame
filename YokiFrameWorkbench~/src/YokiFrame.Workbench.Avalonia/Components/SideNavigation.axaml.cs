using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>
/// Workbench 左侧导航组件，统一承载页面分组、选中态和版本入口。
/// </summary>
public sealed partial class SideNavigation : UserControl
{
    /// <summary>
    /// 创建侧边导航组件并加载 XAML。
    /// </summary>
    public SideNavigation()
    {
        InitializeComponent();
    }
}
