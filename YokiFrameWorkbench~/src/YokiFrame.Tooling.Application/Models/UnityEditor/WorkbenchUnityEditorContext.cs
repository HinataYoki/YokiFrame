namespace YokiFrame.Tooling.Application.Models.UnityEditor;

/// <summary>描述 Workbench 可复用的 Unity Editor 只读上下文。</summary>
public sealed class WorkbenchUnityEditorContext
{
    /// <summary>获取上下文协议版本。</summary>
    public int SchemaVersion { get; init; }

    /// <summary>获取当前上下文是否可用。</summary>
    public bool Available { get; init; }

    /// <summary>获取 Selection、Scene 或 Editor 状态变化 revision。</summary>
    public long Revision { get; init; }

    /// <summary>获取当前 Selection。</summary>
    public WorkbenchUnityEditorSelection Selection { get; init; } = new();

    /// <summary>获取当前活动 Scene。</summary>
    public WorkbenchUnityEditorScene Scene { get; init; } = new();

    /// <summary>获取当前 Prefab Stage。</summary>
    public WorkbenchUnityEditorPrefabStage PrefabStage { get; init; } = new();

    /// <summary>获取当前 Editor 状态。</summary>
    public WorkbenchUnityEditorState Editor { get; init; } = new();

    /// <summary>获取读取或解析失败说明；成功时为空。</summary>
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>描述当前 Unity Selection。</summary>
public sealed class WorkbenchUnityEditorSelection
{
    /// <summary>获取本次返回的对象数量。</summary>
    public int Count { get; init; }

    /// <summary>获取 Unity 原始 Selection 数量。</summary>
    public int TotalCount { get; init; }

    /// <summary>获取 Selection 是否因协议上限被裁剪。</summary>
    public bool Truncated { get; init; }

    /// <summary>获取当前活动对象 GlobalObjectId。</summary>
    public string ActiveGlobalObjectId { get; init; } = string.Empty;

    /// <summary>获取当前活动对象。</summary>
    public WorkbenchUnityEditorObject? ActiveObject { get; init; }

    /// <summary>获取按 Unity Selection 顺序排列的对象。</summary>
    public IReadOnlyList<WorkbenchUnityEditorObject> Objects { get; init; } =
        Array.Empty<WorkbenchUnityEditorObject>();
}

/// <summary>描述一个无 Unity 引用的稳定对象事实。</summary>
public sealed class WorkbenchUnityEditorObject
{
    /// <summary>获取 Unity GlobalObjectId。</summary>
    public string GlobalObjectId { get; init; } = string.Empty;

    /// <summary>获取资产 GUID。</summary>
    public string AssetGuid { get; init; } = string.Empty;

    /// <summary>获取项目相对资产路径。</summary>
    public string AssetPath { get; init; } = string.Empty;

    /// <summary>获取对象名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>获取对象完整类型名。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>获取对象层级路径。</summary>
    public string HierarchyPath { get; init; } = string.Empty;

    /// <summary>获取对象是否来自资产。</summary>
    public bool IsAsset { get; init; }

    /// <summary>获取对象是否为 GameObject 或 Component。</summary>
    public bool IsGameObject { get; init; }
}

/// <summary>描述当前活动 Scene。</summary>
public sealed class WorkbenchUnityEditorScene
{
    /// <summary>获取 Scene 路径。</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>获取 Scene 名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>获取 Scene 是否有未保存修改。</summary>
    public bool Dirty { get; init; }

    /// <summary>获取 Scene Build Settings 索引。</summary>
    public int BuildIndex { get; init; } = -1;
}

/// <summary>描述当前 Prefab Stage。</summary>
public sealed class WorkbenchUnityEditorPrefabStage
{
    /// <summary>获取当前是否正在编辑 Prefab。</summary>
    public bool Active { get; init; }

    /// <summary>获取正在编辑的 Prefab 资产路径。</summary>
    public string AssetPath { get; init; } = string.Empty;

    /// <summary>获取 Prefab Stage 场景路径。</summary>
    public string ScenePath { get; init; } = string.Empty;

    /// <summary>获取 Prefab 内容根名称。</summary>
    public string RootName { get; init; } = string.Empty;
}

/// <summary>描述当前 Unity Editor 生命周期状态。</summary>
public sealed class WorkbenchUnityEditorState
{
    /// <summary>获取当前模式。</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>获取是否正在 Play Mode。</summary>
    public bool IsPlaying { get; init; }

    /// <summary>获取是否暂停。</summary>
    public bool IsPaused { get; init; }

    /// <summary>获取是否正在编译。</summary>
    public bool IsCompiling { get; init; }

    /// <summary>获取是否正在更新资产。</summary>
    public bool IsUpdating { get; init; }

    /// <summary>获取是否为 Batch Mode。</summary>
    public bool IsBatchMode { get; init; }
}
