namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Workbench 中的状态卡片。
/// </summary>
public sealed class WorkbenchMetricCard
{
    /// <summary>
    /// 创建状态卡片。
    /// </summary>
    /// <param name="title">卡片标题。</param>
    /// <param name="value">主要值。</param>
    /// <param name="detail">辅助说明。</param>
    /// <param name="isPositive">是否使用健康态强调色。</param>
    /// <param name="isAccent">是否使用选中/强调态颜色。</param>
    public WorkbenchMetricCard(string title, string value, string detail, bool isPositive = false, bool isAccent = false)
    {
        Title = title;
        Value = string.IsNullOrWhiteSpace(value) ? "--" : value;
        Detail = string.IsNullOrWhiteSpace(detail) ? "--" : detail;
        IsPositive = isPositive;
        IsAccent = isAccent;
    }

    /// <summary>
    /// 获取卡片标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取主要值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 获取辅助说明。
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// 获取是否使用健康态强调色。
    /// </summary>
    public bool IsPositive { get; }

    /// <summary>
    /// 获取是否使用选中/强调态颜色。
    /// </summary>
    public bool IsAccent { get; }
}
