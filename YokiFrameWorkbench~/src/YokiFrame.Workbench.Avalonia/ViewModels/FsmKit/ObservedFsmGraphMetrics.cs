namespace YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

/// <summary>
/// 集中定义 FsmKit 观测图布局与渲染共享的尺寸，避免布局计算和控件绘制产生偏差。
/// </summary>
public static class ObservedFsmGraphMetrics
{
    /// <summary>获取空图和小型图使用的最小画布宽度。</summary>
    public const double DEFAULT_CANVAS_WIDTH = 520.0;

    /// <summary>获取空图和小型图使用的最小画布高度。</summary>
    public const double DEFAULT_CANVAS_HEIGHT = 420.0;

    /// <summary>获取所有状态节点统一使用的绘制宽度。</summary>
    public const double NODE_WIDTH = 136.0;

    /// <summary>获取所有状态节点统一使用的绘制高度。</summary>
    public const double NODE_HEIGHT = 58.0;
}
