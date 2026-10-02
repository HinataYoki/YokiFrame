namespace YokiFrame.Tooling.Application.Services.Settings;

/// <summary>维护 Workbench 可用的项目配置后端；新引擎可以在创建 Store 前注册自己的实现。</summary>
public static class YokiFrameProjectSettingsBackendRegistry
{
    private static readonly object sLock = new();
    private static readonly List<IYokiFrameProjectSettingsBackend> sRegistered = new();

    /// <summary>注册一个引擎项目配置后端；同一实例只保留一次。</summary>
    /// <param name="backend">待注册后端。</param>
    public static void Register(IYokiFrameProjectSettingsBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        YokiFrameProjectSettingsStore.ValidateIdentifier(backend.EngineId, nameof(backend));
        lock (sLock)
        {
            if (!sRegistered.Contains(backend)) sRegistered.Add(backend);
        }
    }

    /// <summary>返回默认后端和外部注册后端的稳定快照。</summary>
    /// <returns>Store 实例独立持有的后端快照。</returns>
    internal static IYokiFrameProjectSettingsBackend[] CreateSnapshot()
    {
        lock (sLock)
        {
            IYokiFrameProjectSettingsBackend[] result = new IYokiFrameProjectSettingsBackend[6 + sRegistered.Count];
            result[0] = new UnityJsonProjectSettingsBackend();
            result[1] = new GodotRuntimeProjectSettingsBackend();
            result[2] = new GodotEditorProjectSettingsBackend();
            result[3] = new GodotEditorUserJsonProjectSettingsBackend();
            result[4] = new HostOwnedDocumentBackend(
                YokiFrameProjectSettingsTarget.TableKitDraft,
                "ProjectSettings/Packages/com.hinatayoki.yokiframe/tablekit-settings.json",
                "project.godot",
                "TableKit");
            result[5] = new HostOwnedDocumentBackend(
                YokiFrameProjectSettingsTarget.LocalizationKitDraft,
                "ProjectSettings/Packages/com.hinatayoki.yokiframe/localizationkit-settings.json",
                "project.godot",
                "LocalizationKit");
            sRegistered.CopyTo(result, 6);
            return result;
        }
    }
}

/// <summary>处理标准 formatVersion/settings JSON 的公共后端实现。</summary>
internal abstract class JsonProjectSettingsBackendBase : IYokiFrameProjectSettingsBackend
{
    /// <summary>获取该 JSON 后端对应的引擎标识。</summary>
    public abstract string EngineId { get; }

    /// <summary>判断目标是否由当前引擎的标准稀疏 JSON 后端处理。</summary>
    /// <param name="target">待判断目标。</param>
    /// <returns>引擎、文档和配置域匹配时返回 true。</returns>
    public bool CanHandle(YokiFrameProjectSettingsTarget target)
    {
        return target.EngineId == EngineId && target.DocumentId == "settings" && CanHandleScope(target.Scope);
    }

    /// <summary>读取标准稀疏 JSON 文档。</summary>
    /// <param name="target">配置目标。</param>
    /// <param name="path">受 Store 守卫的物理路径。</param>
    /// <returns>完整原文和结构化条目。</returns>
    public YokiFrameProjectSettingsBackendDocument Read(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
        return YokiFrameProjectSettingsStore.LoadJsonBackendDocument(target, path);
    }

    /// <summary>序列化标准稀疏 JSON 文档。</summary>
    /// <param name="document">已经应用 patch 的后端文档。</param>
    /// <param name="patches">本次结构化 patch；JSON 格式由文档最终条目决定。</param>
    /// <returns>待原子提交的完整 JSON。</returns>
    public string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        return YokiFrameProjectSettingsStore.SerializeJsonBackendDocument(document.Settings);
    }

    /// <summary>返回 Unity/Godot 各配置域的稳定项目相对路径。</summary>
    /// <param name="target">配置目标。</param>
    /// <param name="projectRoot">当前项目根；JSON 路径由目标自身决定，不依赖项目根探测。</param>
    /// <returns>项目相对路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target, string projectRoot)
    {
        if (target.EngineId == "unity" && target.Scope == YokiFrameProjectSettingsScope.Runtime)
            return "Assets/Settings/Resources/YokiFrame/runtime-settings.json";
        if (target.EngineId == "unity" && target.Scope == YokiFrameProjectSettingsScope.EditorProject)
            return "ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json";
        if (target.EngineId == "unity" && target.Scope == YokiFrameProjectSettingsScope.EditorUser)
            return "UserSettings/YokiFrame/unity-user-settings.json";
        if (target.EngineId == "godot" && target.Scope == YokiFrameProjectSettingsScope.EditorProject)
            return "project.godot";
        if (target.EngineId == "godot" && target.Scope == YokiFrameProjectSettingsScope.EditorUser)
            return ".yokiframe/settings/godot-user-settings.json";
        throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported JSON settings target.");
    }

    /// <summary>判断当前引擎后端允许的配置域。</summary>
    /// <param name="scope">待判断配置域。</param>
    /// <returns>支持该域时返回 true。</returns>
    protected abstract bool CanHandleScope(YokiFrameProjectSettingsScope scope);
}

/// <summary>提供 Unity Runtime/Editor Project/User JSON 后端。</summary>
internal sealed class UnityJsonProjectSettingsBackend : JsonProjectSettingsBackendBase
{
    /// <summary>获取 Unity 引擎标识。</summary>
    public override string EngineId => "unity";

    /// <summary>Unity JSON 后端支持三个配置域。</summary>
    /// <param name="scope">待判断配置域。</param>
    /// <returns>始终返回 true。</returns>
    protected override bool CanHandleScope(YokiFrameProjectSettingsScope scope) => true;
}

/// <summary>保存 Godot 本机用户设置。该文件属于缓存，不进入 Git。</summary>
internal sealed class GodotEditorUserJsonProjectSettingsBackend : JsonProjectSettingsBackendBase
{
    /// <summary>获取 Godot 引擎标识。</summary>
    public override string EngineId => "godot";

    /// <summary>本机 JSON 后端只处理 Editor User 域。</summary>
    /// <param name="scope">待判断配置域。</param>
    /// <returns>是本机用户域时返回 true。</returns>
    protected override bool CanHandleScope(YokiFrameProjectSettingsScope scope) =>
        scope == YokiFrameProjectSettingsScope.EditorUser;
}

/// <summary>提供 Godot Editor Project 的 project.godot section 后端；本机用户设置仍使用 JSON。</summary>
internal sealed class GodotEditorProjectSettingsBackend : IYokiFrameProjectSettingsBackend
{
    /// <summary>获取 Godot 引擎标识。</summary>
    public string EngineId => "godot";

    /// <summary>仅匹配 Godot Editor Project 的 settings 文档。</summary>
    /// <param name="target">待判断目标。</param>
    /// <returns>匹配时返回 true。</returns>
    public bool CanHandle(YokiFrameProjectSettingsTarget target)
    {
        return target.EngineId == EngineId
               && target.Scope == YokiFrameProjectSettingsScope.EditorProject
               && target.DocumentId == "settings";
    }

    /// <summary>读取 project.godot 的 YokiFrame Editor section。</summary>
    /// <param name="target">Godot Editor 目标。</param>
    /// <param name="path">project.godot 绝对路径。</param>
    /// <returns>保留完整原文的结构化文档。</returns>
    public YokiFrameProjectSettingsBackendDocument Read(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
        return YokiFrameProjectSettingsStore.LoadGodotBackendDocument(
            target,
            path,
            YokiFrameProjectSettingsStore.GODOT_EDITOR_SECTION);
    }

    /// <summary>只更新 YokiFrame Editor section，并保留其它 project.godot 原文。</summary>
    /// <param name="document">原始 Godot 文档。</param>
    /// <param name="patches">Editor owner patch。</param>
    /// <returns>保留其它 section 的完整文本。</returns>
    public string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        return YokiFrameProjectSettingsStore.SerializeGodotBackendDocument(
            document.OriginalText,
            YokiFrameProjectSettingsStore.GODOT_EDITOR_SECTION,
            patches);
    }

    /// <summary>返回 Godot 项目配置文件相对路径。</summary>
    /// <param name="target">Godot Editor 目标。</param>
    /// <param name="projectRoot">当前项目根。</param>
    /// <returns>固定 project.godot 路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target, string projectRoot) => "project.godot";
}

/// <summary>处理 Godot `project.godot` 中的 YokiFrame Runtime section。</summary>
internal sealed class GodotRuntimeProjectSettingsBackend : IYokiFrameProjectSettingsBackend
{
    /// <summary>获取 Godot 引擎标识。</summary>
    public string EngineId => "godot";

    /// <summary>仅匹配 Godot Runtime project 文档。</summary>
    /// <param name="target">待判断目标。</param>
    /// <returns>匹配 Godot Runtime project 文档时返回 true。</returns>
    public bool CanHandle(YokiFrameProjectSettingsTarget target)
    {
        return target.EngineId == EngineId
               && target.Scope == YokiFrameProjectSettingsScope.Runtime
               && target.DocumentId == "project";
    }

    /// <summary>读取并投影 Godot Runtime section。</summary>
    /// <param name="target">Godot Runtime 目标。</param>
    /// <param name="path">project.godot 绝对路径。</param>
    /// <returns>保留完整原文的结构化文档。</returns>
    public YokiFrameProjectSettingsBackendDocument Read(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
        return YokiFrameProjectSettingsStore.LoadGodotBackendDocument(
            target,
            path,
            YokiFrameProjectSettingsStore.GODOT_RUNTIME_SECTION);
    }

    /// <summary>只更新 YokiFrame Runtime section 并保留其它 project.godot 原文。</summary>
    /// <param name="document">原始 Godot 文档。</param>
    /// <param name="patches">YokiFrame Runtime owner patch。</param>
    /// <returns>保留其它 section 的完整 project.godot 文本。</returns>
    public string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        return YokiFrameProjectSettingsStore.SerializeGodotBackendDocument(
            document.OriginalText,
            YokiFrameProjectSettingsStore.GODOT_RUNTIME_SECTION,
            patches);
    }

    /// <summary>返回 Godot 项目配置文件相对路径。</summary>
    /// <param name="target">Godot Runtime 目标。</param>
    /// <param name="projectRoot">当前项目根；Runtime 始终写入 project.godot。</param>
    /// <returns>固定 project.godot 路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target, string projectRoot) => "project.godot";
}

/// <summary>
/// 处理跨引擎 Workbench 独占草稿；内容只通过 owned-document Gateway 读写。
/// Unity 保留独立 JSON，Godot 共享草稿写入 project.godot，避免制造 Unity 目录或不可同步缓存。
/// </summary>
internal sealed class HostOwnedDocumentBackend : IYokiFrameProjectOwnedDocumentBackend
{
    private readonly YokiFrameProjectSettingsTarget mTarget;
    private readonly string mUnityRelativePath;
    private readonly string mGodotRelativePath;
    private readonly string mDisplayName;

    /// <summary>创建按宿主选择物理路径的 owned-document 后端。</summary>
    /// <param name="target">后端唯一拥有的配置目标。</param>
    /// <param name="unityRelativePath">Unity 项目内稳定文档路径。</param>
    /// <param name="godotRelativePath">Godot 项目内稳定文档路径。</param>
    /// <param name="displayName">用于错误诊断的 Kit 名称。</param>
    public HostOwnedDocumentBackend(
        YokiFrameProjectSettingsTarget target,
        string unityRelativePath,
        string godotRelativePath,
        string displayName)
    {
        mTarget = target ?? throw new ArgumentNullException(nameof(target));
        mUnityRelativePath = unityRelativePath ?? throw new ArgumentNullException(nameof(unityRelativePath));
        mGodotRelativePath = godotRelativePath ?? throw new ArgumentNullException(nameof(godotRelativePath));
        mDisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    /// <summary>获取跨引擎共享 Workbench 文档的引擎标识。</summary>
    public string EngineId => mTarget.EngineId;

    /// <summary>仅匹配构造时指定的 Workbench 草稿文档。</summary>
    /// <param name="target">待判断目标。</param>
    /// <returns>目标标识一致时返回 true。</returns>
    public bool CanHandle(YokiFrameProjectSettingsTarget target) =>
        target != null && string.Equals(target.Id, mTarget.Id, StringComparison.Ordinal);

    /// <summary>读取原始 Workbench 草稿；Godot 从 project.godot 的对应键取出 JSON 原文。</summary>
    /// <param name="target">当前草稿目标。</param>
    /// <param name="path">草稿绝对路径。</param>
    /// <returns>包含完整草稿原文的 owned-document。</returns>
    public YokiFrameProjectSettingsBackendDocument Read(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
        if (UsesGodotProject(path)) return ReadGodotDraft(target, path);
        if (!File.Exists(path))
        {
            return new YokiFrameProjectSettingsBackendDocument(
                target, path, false, string.Empty, Array.Empty<YokiFrameProjectSetting>());
        }

        byte[] bytes = YokiFrameProjectSettingsStore.ReadBoundedFile(path);
        string content = System.Text.Encoding.UTF8.GetString(bytes);
        return new YokiFrameProjectSettingsBackendDocument(
            target,
            path,
            true,
            content,
            Array.Empty<YokiFrameProjectSetting>(),
            YokiFrameProjectSettingsStore.ComputeFingerprint(bytes));
    }

    /// <summary>从 Godot Editor section 读取一个草稿键，文件存在但键缺失时返回空草稿。</summary>
    private YokiFrameProjectSettingsBackendDocument ReadGodotDraft(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
        if (!File.Exists(path))
        {
            return new YokiFrameProjectSettingsBackendDocument(
                target, path, false, string.Empty, Array.Empty<YokiFrameProjectSetting>());
        }

        byte[] bytes = YokiFrameProjectSettingsStore.ReadBoundedFile(path);
        string project = System.Text.Encoding.UTF8.GetString(bytes);
        bool hasValue = YokiFrameProjectSettingsStore.TryReadGodotValue(
            project,
            YokiFrameProjectSettingsStore.GODOT_EDITOR_SECTION,
            mTarget.DocumentId,
            "document",
            out string content);
        return new YokiFrameProjectSettingsBackendDocument(
            target,
            path,
            hasValue,
            hasValue ? content : string.Empty,
            Array.Empty<YokiFrameProjectSetting>(),
            YokiFrameProjectSettingsStore.ComputeFingerprint(bytes));
    }

    /// <summary>把 owned-document Gateway 提交的完整草稿写回 Godot Editor section 的单键。</summary>
    /// <param name="document">当前 project.godot 原文。</param>
    /// <param name="patches">只允许包含一个 document 键的草稿 patch。</param>
    /// <returns>只更新目标键后的完整 project.godot。</returns>
    public string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        if (!UsesGodotProject(document.Path))
        {
            throw new InvalidOperationException(mDisplayName + " draft must be written through the owned-document Gateway.");
        }

        return YokiFrameProjectSettingsStore.SerializeGodotBackendDocument(
            document.OriginalText,
            YokiFrameProjectSettingsStore.GODOT_EDITOR_SECTION,
            patches);
    }

    /// <summary>判断当前物理路径是否是 Godot 项目设置，而不是 Unity JSON。</summary>
    private static bool UsesGodotProject(string path)
    {
        return string.Equals(Path.GetFileName(path), "project.godot", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>按项目根已有的宿主标记选择草稿路径；Godot 优先于 Unity。</summary>
    /// <param name="target">当前草稿目标。</param>
    /// <param name="projectRoot">已规范化的当前项目根。</param>
    /// <returns>当前宿主下的稳定草稿路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target, string projectRoot)
    {
        return YokiFrameProjectHostLayout.IsGodotProject(projectRoot)
            ? mGodotRelativePath
            : mUnityRelativePath;
    }
}
