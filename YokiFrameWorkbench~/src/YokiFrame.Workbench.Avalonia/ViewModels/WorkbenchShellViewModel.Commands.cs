using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 维护 Workbench Shell 的快捷命令目录和选择状态。
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
        var catalog = ParseCommandCatalogJson(resultJson);
        if (catalog.Count == 0)
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
    private void ReplaceCommandCatalog(Dictionary<string, IReadOnlyList<string>> catalog)
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

    /// <summary>
    /// 创建离线或目录读取失败时使用的基础 System 命令目录。
    /// </summary>
    /// <returns>基础命令目录。</returns>
    private static Dictionary<string, IReadOnlyList<string>> CreateFallbackCommandCatalog()
    {
        return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["System"] = new[]
            {
                "ping",
                "bridge_status",
                "list_commands",
                "refresh_snapshots",
                "get_environment",
                "open_project_folder",
                "open_log"
            }
        };
    }

    /// <summary>
    /// 解析 System/list_commands 返回的 JSON，并过滤不安全的 Kit/action 标识。
    /// </summary>
    /// <param name="resultJson">命令目录 JSON。</param>
    /// <returns>解析后的命令目录。</returns>
    private static Dictionary<string, IReadOnlyList<string>> ParseCommandCatalogJson(string resultJson)
    {
        Dictionary<string, IReadOnlyList<string>> catalog = new(StringComparer.Ordinal);
        try
        {
            var kits = JsonNode.Parse(resultJson)?["kits"]?.AsArray();
            if (kits == null)
            {
                return catalog;
            }

            foreach (var kitNode in kits)
            {
                AddCommandCatalogKit(catalog, kitNode);
            }
        }
        catch (JsonException)
        {
            return catalog;
        }

        return catalog;
    }

    /// <summary>
    /// 从单个 Kit 节点提取 action 列表，并跳过不安全标识。
    /// </summary>
    /// <param name="catalog">待填充的命令目录。</param>
    /// <param name="kitNode">Kit JSON 节点。</param>
    private static void AddCommandCatalogKit(Dictionary<string, IReadOnlyList<string>> catalog, JsonNode? kitNode)
    {
        var kit = kitNode?["kit"]?.GetValue<string>() ?? string.Empty;
        var actions = kitNode?["actions"]?.AsArray();
        if (!IsSafeCommandIdentifier(kit) || actions == null)
        {
            return;
        }

        List<string> actionNames = new();
        foreach (var actionNode in actions)
        {
            var action = actionNode?["action"]?.GetValue<string>() ?? string.Empty;
            if (IsSafeCommandIdentifier(action))
            {
                actionNames.Add(action);
            }
        }

        if (actionNames.Count > 0)
        {
            catalog[kit] = actionNames;
        }
    }

    /// <summary>
    /// 判断命令目录中的 Kit/action 是否可安全用于 UI 和 FileBridge 请求。
    /// </summary>
    /// <param name="value">待检查标识。</param>
    /// <returns>安全时返回 true。</returns>
    private static bool IsSafeCommandIdentifier(string value)
    {
        return YokiFrameSafeIdContract.IsSafeId(value);
    }
}
