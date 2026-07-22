namespace YokiFrame.Tooling.Application.Models.UIKit;

/// <summary>列出 UIKit Workbench 允许触发的 Unity Editor 显式操作。</summary>
public enum WorkbenchUIKitEditorAction
{
    /// <summary>只读刷新当前 Unity 选择和默认生成参数。</summary>
    RefreshContext,

    /// <summary>创建 Panel Prefab 与初始代码。</summary>
    CreatePanelPrefab,

    /// <summary>为当前选中的 Panel Prefab 重新生成代码。</summary>
    GenerateCodeForSelection,

    /// <summary>为 Unity 当前选择添加 Bind。</summary>
    AddBindToSelection,

    /// <summary>从 Unity 当前选择移除 Bind。</summary>
    RemoveBindFromSelection,
}

/// <summary>保存 UIKit Panel 生成表单的强类型参数。</summary>
public sealed class WorkbenchUIKitPanelGenerationRequest
{
    /// <summary>获取或设置 Panel C# 类型名。</summary>
    public string PanelName { get; set; } = string.Empty;

    /// <summary>获取或设置 Prefab 的 Assets 相对目录。</summary>
    public string PrefabFolder { get; set; } = "Assets/Resources/Art/UIPrefab";

    /// <summary>获取或设置代码的 Assets 相对目录。</summary>
    public string ScriptFolder { get; set; } = "Assets/Scripts/UI";

    /// <summary>获取或设置生成代码命名空间。</summary>
    public string ScriptNamespace { get; set; } = "GameUI";

    /// <summary>获取或设置生成类型所在程序集。</summary>
    public string AssemblyName { get; set; } = "Assembly-CSharp";

    /// <summary>获取或设置 Unity Editor 当前可用的安全模板名。</summary>
    public string CodeTemplate { get; set; } = "Default";

    /// <summary>获取或设置读取 Selection 时记录的 Unity Editor Context revision；零表示兼容旧调用方。</summary>
    public long ExpectedContextRevision { get; set; }

    /// <summary>获取或设置当前活动对象的稳定 GlobalObjectId。</summary>
    public string TargetGlobalObjectId { get; set; } = string.Empty;
}

/// <summary>描述当前 Unity Editor 选择和可执行 UIKit 工具。</summary>
public sealed class WorkbenchUIKitEditorContext
{
    /// <summary>获取 Editor 工具是否在线。</summary>
    public bool Available { get; init; }

    /// <summary>获取 Unity Editor 公共上下文 revision。</summary>
    public long ContextRevision { get; init; }

    /// <summary>获取当前活动对象的稳定 GlobalObjectId。</summary>
    public string ActiveGlobalObjectId { get; init; } = string.Empty;

    /// <summary>获取当前选择资产路径。</summary>
    public string SelectedAssetPath { get; init; } = string.Empty;

    /// <summary>获取当前选择对象名称。</summary>
    public string SelectedObjectName { get; init; } = string.Empty;

    /// <summary>获取选中的 GameObject 数量。</summary>
    public int SelectedGameObjectCount { get; init; }

    /// <summary>获取选中对象中的 Bind 数量。</summary>
    public int SelectedBindCount { get; init; }

    /// <summary>获取当前选择是否可生成代码。</summary>
    public bool CanGenerateCode { get; init; }

    /// <summary>获取当前选择是否可添加 Bind。</summary>
    public bool CanAddBind { get; init; }

    /// <summary>获取当前选择是否可移除 Bind。</summary>
    public bool CanRemoveBind { get; init; }

    /// <summary>获取 Unity Provider 建议的默认生成请求。</summary>
    public WorkbenchUIKitPanelGenerationRequest Defaults { get; init; } = new();

    /// <summary>
    /// 获取 Unity Editor 当前 Registry 暴露的模板名；缺少该字段的旧 Provider 使用内置两项。
    /// </summary>
    public IReadOnlyList<string> CodeTemplateOptions { get; init; } = new[] { "Default", "Minimal" };

    /// <summary>
    /// 获取 Unity Editor 扫描到的可用于生成代码的 Player 程序集；旧 Provider 缺少该字段时保留默认程序集。
    /// </summary>
    public IReadOnlyList<string> AssemblyNames { get; init; } = new[] { "Assembly-CSharp" };

    /// <summary>获取当前活动 Scene 路径。</summary>
    public string ScenePath { get; init; } = string.Empty;

    /// <summary>获取当前 Prefab Stage 是否活动。</summary>
    public bool PrefabStageActive { get; init; }

    /// <summary>获取当前 Editor 模式。</summary>
    public string EditorMode { get; init; } = string.Empty;
}

/// <summary>保存一次 UIKit Editor action 的强类型结果和最新选择上下文。</summary>
public sealed class WorkbenchUIKitEditorResult
{
    /// <summary>获取操作是否成功。</summary>
    public bool Succeeded { get; init; }

    /// <summary>获取稳定 action。</summary>
    public WorkbenchUIKitEditorAction Action { get; init; }

    /// <summary>获取用户可见消息。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>获取生成或选择操作影响数量。</summary>
    public int AffectedCount { get; init; }

    /// <summary>获取生成 Prefab 路径。</summary>
    public string PrefabPath { get; init; } = string.Empty;

    /// <summary>获取生成 Panel 用户脚本路径。</summary>
    public string PanelScriptPath { get; init; } = string.Empty;

    /// <summary>获取生成 Designer 路径。</summary>
    public string DesignerScriptPath { get; init; } = string.Empty;

    /// <summary>获取操作后回读的 Unity 选择上下文。</summary>
    public WorkbenchUIKitEditorContext? Context { get; init; }
}
