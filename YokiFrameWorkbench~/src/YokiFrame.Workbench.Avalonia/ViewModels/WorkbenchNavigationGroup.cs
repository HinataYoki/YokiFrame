namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Workbench 左侧导航中的一个分组。
/// </summary>
public sealed class WorkbenchNavigationGroup
{
    /// <summary>
    /// 创建导航分组。
    /// </summary>
    /// <param name="title">分组标题。</param>
    /// <param name="items">分组内导航项。</param>
    public WorkbenchNavigationGroup(string title, IReadOnlyList<WorkbenchNavigationItem> items)
    {
        Title = title;
        Items = items;
    }

    /// <summary>
    /// 获取分组标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取分组内导航项。
    /// </summary>
    public IReadOnlyList<WorkbenchNavigationItem> Items { get; }
}
