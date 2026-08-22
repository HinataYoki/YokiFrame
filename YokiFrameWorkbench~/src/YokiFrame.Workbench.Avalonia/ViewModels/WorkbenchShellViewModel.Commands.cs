using YokiFrame.Tooling.Application.Capabilities;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 维护 Workbench Shell 的快捷命令目录和选择状态。
/// wire JSON 解析统一委托 <see cref="CommandCatalogReader"/>，ViewModel 不直接解析协议 payload。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    private readonly Dictionary<string, IReadOnlyList<string>> mCommandCatalog = new(StringComparer.Ordinal);
    private IReadOnlyList<string> mCommandActions = Array.Empty<string>();
    private IReadOnlyList<string> mCommandGroups = Array.Empty<string>();
    private string mCommandAction = "ping";
    private string mCommandGroup = "System";

    /// <summary>
    /// 获取或设置快速命令分组。
    /// </summary>
    public string CommandGroup
    {
        get => mCommandGroup;
        set
        {
            var nextGroup = string.IsNullOrWhiteSpace(value) ? "System" : value;
            if (SetProperty(ref mCommandGroup, nextGroup))
            {
                RefreshCommandActions();
            }
        }
    }

    /// <summary>
    /// 获取或设置快速命令 action。
    /// </summary>
    public string CommandAction
    {
        get => mCommandAction;
        set => SetProperty(ref mCommandAction, string.IsNullOrWhiteSpace(value) ? "ping" : value);
    }

    /// <summary>
    /// 获取快速命令分组选项。
    /// </summary>
    public IReadOnlyList<string> CommandGroups
    {
        get => mCommandGroups;
        private set => SetProperty(ref mCommandGroups, value);
    }

    /// <summary>
    /// 获取快速命令 action 选项。
    /// </summary>
    public IReadOnlyList<string> CommandActions
    {
        get => mCommandActions;
        private set => SetProperty(ref mCommandActions, value);
    }

    /// <summary>
    /// 从 System/list_commands 的 resultJson 更新快捷命令目录；无效响应会保留当前目录。
    /// </summary>
    /// <param name="resultJson">命令目录 JSON。</param>
    public void UpdateCommandCatalogJson(string resultJson)
    {
        // wire JSON 解析与安全过滤统一在 Application 读取器内完成。
        if (!CommandCatalogReader.TryRead(resultJson, out var catalog))
        {
            return;
        }

        ReplaceCommandCatalog(catalog);
        AddLogLine("命令目录已刷新。");
    }

    /// <summary>
    /// 替换快捷命令目录，并保持当前选择尽量稳定。
    /// </summary>
    /// <param name="catalog">新的命令目录。</param>
    private void ReplaceCommandCatalog(IReadOnlyDictionary<string, IReadOnlyList<string>> catalog)
    {
        mCommandCatalog.Clear();
        foreach (var entry in catalog)
        {
            mCommandCatalog[entry.Key] = entry.Value;
        }

        CommandGroups = mCommandCatalog.Keys.ToArray();
        CommandGroup = mCommandCatalog.ContainsKey(CommandGroup) ? CommandGroup : CommandGroups[0];
        RefreshCommandActions();
    }

    /// <summary>
    /// 根据当前 Kit 分组刷新 action 列表，并在 action 不可用时选择第一个可用项。
    /// </summary>
    private void RefreshCommandActions()
    {
        if (!mCommandCatalog.TryGetValue(CommandGroup, out var actions) || actions.Count == 0)
        {
            CommandActions = Array.Empty<string>();
            CommandAction = string.Empty;
            return;
        }

        CommandActions = actions;
        if (!actions.Contains(CommandAction, StringComparer.Ordinal))
        {
            CommandAction = actions[0];
        }
    }
}
