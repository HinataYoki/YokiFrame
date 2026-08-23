using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 维护 Workbench 的显式编译期页面清单，并统一生成页面名和导航分组。
/// </summary>
public sealed class WorkbenchPageModuleCatalog
{
    private readonly IReadOnlyDictionary<string, WorkbenchPageModule> mModulesByName;

    /// <summary>
    /// 创建页面 Catalog，并立即校验空清单、重复页面名和默认页缺失。
    /// </summary>
    /// <param name="modules">按导航顺序声明的页面模块。</param>
    /// <param name="defaultPageName">默认页面内部名称。</param>
    public WorkbenchPageModuleCatalog(
        IEnumerable<WorkbenchPageModule> modules,
        string defaultPageName)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultPageName);
        var moduleList = modules.ToArray();
        if (moduleList.Length == 0)
        {
            throw new ArgumentException("Workbench 页面清单不能为空。", nameof(modules));
        }

        Dictionary<string, WorkbenchPageModule> modulesByName = new(StringComparer.Ordinal);
        foreach (var module in moduleList)
        {
            if (!modulesByName.TryAdd(module.PageName, module))
            {
                throw new ArgumentException("Workbench 页面名重复：" + module.PageName, nameof(modules));
            }
        }

        if (!modulesByName.TryGetValue(defaultPageName, out var defaultModule))
        {
            throw new ArgumentException("Workbench 默认页面不存在：" + defaultPageName, nameof(defaultPageName));
        }

        Modules = moduleList;
        PageNames = moduleList.Select(static module => module.PageName).ToArray();
        NavigationPageNames = moduleList
            .Where(static module => module.NavigationVisibility == WorkbenchPageNavigationVisibility.Primary)
            .Select(static module => module.PageName)
            .ToArray();
        DefaultPageName = defaultPageName;
        DefaultModule = defaultModule;
        mModulesByName = modulesByName;
    }

    /// <summary>
    /// 获取按声明顺序排列的页面模块。
    /// </summary>
    public IReadOnlyList<WorkbenchPageModule> Modules { get; }

    /// <summary>
    /// 获取按声明顺序排列的页面内部名称。
    /// </summary>
    public IReadOnlyList<string> PageNames { get; }

    /// <summary>
    /// 获取实际显示在左侧一级导航中的页面名称。
    /// </summary>
    public IReadOnlyList<string> NavigationPageNames { get; }

    /// <summary>
    /// 获取默认页面内部名称。
    /// </summary>
    public string DefaultPageName { get; }

    /// <summary>
    /// 获取默认页面模块。
    /// </summary>
    public WorkbenchPageModule DefaultModule { get; }

    /// <summary>
    /// 查找页面模块；空值或未知名称返回 null，供 Shell 回落默认页。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <returns>匹配模块；不存在时为 null。</returns>
    public WorkbenchPageModule? Find(string? pageName)
    {
        return !string.IsNullOrWhiteSpace(pageName)
            && mModulesByName.TryGetValue(pageName, out var module)
                ? module
                : null;
    }

    /// <summary>
    /// 获取指定页面模块；页面不存在时抛出带名称的明确异常。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <returns>匹配模块。</returns>
    public WorkbenchPageModule GetRequired(string pageName)
    {
        return Find(pageName)
            ?? throw new KeyNotFoundException("Workbench 页面不存在：" + pageName);
    }

    /// <summary>
    /// 按模块首次出现的分组顺序创建新的可变导航项，避免跨 Shell 共享选中态。
    /// </summary>
    /// <returns>稳定排序的导航分组。</returns>
    public IReadOnlyList<WorkbenchNavigationGroup> CreateNavigationGroups() =>
        CreateLocalizedNavigationGroups(null, null);

    /// <summary>
    /// 按模块首次出现的分组顺序创建新的可变导航项，并支持自定义本地化函数。
    /// </summary>
    /// <param name="groupNameLocalizer">可选的分组名称本地化函数。</param>
    /// <param name="itemNameLocalizer">可选的导航项名称本地化函数。</param>
    /// <returns>稳定排序的导航分组。</returns>
    public IReadOnlyList<WorkbenchNavigationGroup> CreateLocalizedNavigationGroups(
        Func<string, string>? groupNameLocalizer,
        Func<string, string, string>? itemNameLocalizer)
    {
        Dictionary<string, List<WorkbenchNavigationItem>> itemsByGroup = new(StringComparer.Ordinal);
        List<string> groupOrder = new();
        foreach (var module in Modules)
        {
            if (module.NavigationVisibility != WorkbenchPageNavigationVisibility.Primary)
            {
                continue;
            }

            if (!itemsByGroup.TryGetValue(module.GroupName, out var items))
            {
                items = new List<WorkbenchNavigationItem>();
                itemsByGroup.Add(module.GroupName, items);
                groupOrder.Add(module.GroupName);
            }

            var displayName = itemNameLocalizer != null
                ? itemNameLocalizer(module.PageName, module.DisplayName)
                : module.DisplayName;
            items.Add(new WorkbenchNavigationItem(module.PageName, displayName, module.IconKey));
        }

        List<WorkbenchNavigationGroup> groups = new();
        foreach (var groupName in groupOrder)
        {
            var localizedGroupName = groupNameLocalizer != null ? groupNameLocalizer(groupName) : groupName;
            groups.Add(new WorkbenchNavigationGroup(localizedGroupName, itemsByGroup[groupName]));
        }

        return groups;
    }
}
