using System.Text;

namespace YokiFrame.Tooling.Application.Models.UIKit;

/// <summary>
/// UIKit Workbench 只读诊断文案与集合键投影；无 UI 依赖，供 Avalonia 与测试共用。
/// </summary>
public static class WorkbenchUIKitPresentation
{
    /// <summary>
    /// 构造完整 UIKit 诊断文本，保持字段顺序稳定。
    /// </summary>
    public static string CreateSnapshotText(WorkbenchUIKitState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        StringBuilder builder = new();
        builder.AppendLine("UIKit 运行时诊断");
        builder.AppendLine("引擎: " + state.EngineId);
        builder.AppendLine("数据来源: " + state.Source);
        builder.AppendLine("根节点: " + state.Root.Exists);
        builder.AppendLine("面板: " + state.PanelReturned + "/" + state.PanelTotal);
        builder.AppendLine("命名栈: " + state.StackReturned + "/" + state.StackTotal);
        for (int index = 0; index < state.Panels.Count; index++)
        {
            builder.AppendLine(CreatePanelText(state.Panels[index]));
        }

        for (int index = 0; index < state.Stacks.Count; index++)
        {
            builder.AppendLine(CreateStackText(state.Stacks[index]));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 构造单个面板的稳定只读文本。
    /// </summary>
    public static string CreatePanelText(WorkbenchUIKitPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return "面板 " + panel.Type + " | 名称=" + panel.Name + " | 状态=" + panel.State
            + " | 层级=" + panel.Level + "/" + panel.LevelOrder + "/" + panel.SubLevel
            + " | 缓存=" + panel.CachePolicy + " | 模态=" + panel.IsModal
            + " | 命名栈=" + (panel.StackName ?? "无");
    }

    /// <summary>
    /// 构造单个命名栈的稳定只读文本。
    /// </summary>
    public static string CreateStackText(WorkbenchUIKitStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        return "命名栈 " + stack.Name + " | 深度=" + stack.Depth
            + " | 栈顶类型=" + (stack.TopPanelType ?? "无")
            + " | 栈顶名称=" + (stack.TopPanelName ?? "无");
    }

    /// <summary>
    /// 构造当前集合覆盖率文本。
    /// </summary>
    public static string CreateCoverageText(int returned, int total)
    {
        return "显示 " + returned + " / " + total;
    }

    /// <summary>
    /// 构造面板选择稳定键。
    /// </summary>
    public static string CreatePanelKey(WorkbenchUIKitPanel? panel)
    {
        return panel == null ? string.Empty : panel.Type + "\u001f" + panel.Name;
    }

    /// <summary>
    /// 按 Workbench 列表约定排序面板。
    /// </summary>
    public static IReadOnlyList<WorkbenchUIKitPanel> OrderPanels(IEnumerable<WorkbenchUIKitPanel> panels)
    {
        ArgumentNullException.ThrowIfNull(panels);
        return panels
            .OrderBy(static panel => panel.LevelOrder)
            .ThenBy(static panel => panel.SubLevel)
            .ThenBy(static panel => panel.Type, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 按 Workbench 列表约定排序命名栈。
    /// </summary>
    public static IReadOnlyList<WorkbenchUIKitStack> OrderStacks(IEnumerable<WorkbenchUIKitStack> stacks)
    {
        ArgumentNullException.ThrowIfNull(stacks);
        return stacks
            .OrderByDescending(static stack => stack.Depth)
            .ThenBy(static stack => stack.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 把 Editor action 协议枚举转换为简体中文操作名称。
    /// </summary>
    public static string GetEditorActionDisplayName(WorkbenchUIKitEditorAction action)
    {
        return action switch
        {
            WorkbenchUIKitEditorAction.RefreshContext => "刷新当前选择",
            WorkbenchUIKitEditorAction.CreatePanelPrefab => "创建面板预制体",
            WorkbenchUIKitEditorAction.GenerateCodeForSelection => "生成绑定代码",
            WorkbenchUIKitEditorAction.AddBindToSelection => "添加 Bind",
            WorkbenchUIKitEditorAction.RemoveBindFromSelection => "移除 Bind",
            _ => "执行编辑器操作",
        };
    }

    /// <summary>
    /// 从页面表单字段构造 Application 强类型生成请求。
    /// </summary>
    public static WorkbenchUIKitPanelGenerationRequest CreateGenerationRequest(
        string panelName,
        string prefabFolder,
        string scriptFolder,
        string scriptNamespace,
        string assemblyName,
        string codeTemplate,
        long expectedContextRevision,
        string targetGlobalObjectId)
    {
        return new WorkbenchUIKitPanelGenerationRequest
        {
            PanelName = panelName ?? string.Empty,
            PrefabFolder = prefabFolder ?? string.Empty,
            ScriptFolder = scriptFolder ?? string.Empty,
            ScriptNamespace = scriptNamespace ?? string.Empty,
            AssemblyName = assemblyName ?? string.Empty,
            CodeTemplate = string.IsNullOrWhiteSpace(codeTemplate) ? "Default" : codeTemplate,
            ExpectedContextRevision = expectedContextRevision,
            TargetGlobalObjectId = targetGlobalObjectId ?? string.Empty,
        };
    }
}
