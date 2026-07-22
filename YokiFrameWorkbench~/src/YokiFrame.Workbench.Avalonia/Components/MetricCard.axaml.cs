using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>
/// 统一的指标卡组件，供引擎状态、Skill 状态和摘要区域复用。
/// </summary>
public sealed partial class MetricCard : UserControl
{
    /// <summary>
    /// 创建指标卡组件并加载 XAML。
    /// </summary>
    public MetricCard()
    {
        InitializeComponent();
    }
}
