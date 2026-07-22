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
            IYokiFrameProjectSettingsBackend[] result = new IYokiFrameProjectSettingsBackend[5 + sRegistered.Count];
            result[0] = new UnityJsonProjectSettingsBackend();
            result[1] = new GodotRuntimeProjectSettingsBackend();
            result[2] = new GodotEditorJsonProjectSettingsBackend();
            result[3] = new SharedWorkbenchOwnedDocumentBackend(
                YokiFrameProjectSettingsTarget.TableKitDraft,
                "ProjectSettings/Packages/com.hinatayoki.yokiframe/tablekit-settings.json",
                "TableKit");
            result[4] = new SharedWorkbenchOwnedDocumentBackend(
                YokiFrameProjectSettingsTarget.LocalizationKitDraft,
                "ProjectSettings/Packages/com.hinatayoki.yokiframe/localizationkit-settings.json",
                "LocalizationKit");
            sRegistered.CopyTo(result, 5);
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
    /// <returns>项目相对路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target)
    {
        if (target.EngineId == "unity" && target.Scope == YokiFrameProjectSettingsScope.Runtime)
            return "Assets/Settings/Resources/YokiFrame/runtime-settings.json";
        if (target.EngineId == "unity" && target.Scope == YokiFrameProjectSettingsScope.EditorProject)
            return "ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json";
        if (target.EngineId == "unity" && target.Scope == YokiFrameProjectSettingsScope.EditorUser)
            return "UserSettings/YokiFrame/unity-user-settings.json";
        if (target.EngineId == "godot" && target.Scope == YokiFrameProjectSettingsScope.EditorProject)
            return "ProjectSettings/Packages/com.hinatayoki.yokiframe/godot-editor-settings.json";
        if (target.EngineId == "godot" && target.Scope == YokiFrameProjectSettingsScope.EditorUser)
            return "UserSettings/YokiFrame/godot-user-settings.json";
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

/// <summary>提供 Godot Editor Project/User JSON 后端；Runtime 仍由 project.godot 后端负责。</summary>
internal sealed class GodotEditorJsonProjectSettingsBackend : JsonProjectSettingsBackendBase
{
    /// <summary>获取 Godot 引擎标识。</summary>
    public override string EngineId => "godot";

    /// <summary>Godot JSON 后端只支持 Editor Project/User 域。</summary>
    /// <param name="scope">待判断配置域。</param>
    /// <returns>非 Runtime 域时返回 true。</returns>
    protected override bool CanHandleScope(YokiFrameProjectSettingsScope scope) =>
        scope != YokiFrameProjectSettingsScope.Runtime;
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
        return YokiFrameProjectSettingsStore.LoadGodotBackendDocument(target, path);
    }

    /// <summary>只更新 YokiFrame Runtime section 并保留其它 project.godot 原文。</summary>
    /// <param name="document">原始 Godot 文档。</param>
    /// <param name="patches">YokiFrame Runtime owner patch。</param>
    /// <returns>保留其它 section 的完整 project.godot 文本。</returns>
    public string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        return YokiFrameProjectSettingsStore.SerializeGodotBackendDocument(document.OriginalText, patches);
    }

    /// <summary>返回 Godot 项目配置文件相对路径。</summary>
    /// <param name="target">Godot Runtime 目标。</param>
    /// <returns>固定 project.godot 路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target) => "project.godot";
}

/// <summary>处理跨引擎 Workbench 独占草稿；内容只通过 owned-document Gateway 读写。</summary>
internal sealed class SharedWorkbenchOwnedDocumentBackend : IYokiFrameProjectOwnedDocumentBackend
{
    private readonly YokiFrameProjectSettingsTarget mTarget;
    private readonly string mRelativePath;
    private readonly string mDisplayName;

    /// <summary>创建绑定单一 Workbench 文档的通用 owned-document 后端。</summary>
    /// <param name="target">后端唯一拥有的配置目标。</param>
    /// <param name="relativePath">项目内稳定文档路径。</param>
    /// <param name="displayName">用于错误诊断的 Kit 名称。</param>
    public SharedWorkbenchOwnedDocumentBackend(
        YokiFrameProjectSettingsTarget target,
        string relativePath,
        string displayName)
    {
        mTarget = target ?? throw new ArgumentNullException(nameof(target));
        mRelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        mDisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    /// <summary>获取跨引擎共享 Workbench 文档的引擎标识。</summary>
    public string EngineId => mTarget.EngineId;

    /// <summary>仅匹配构造时指定的 Workbench 草稿文档。</summary>
    /// <param name="target">待判断目标。</param>
    /// <returns>目标标识一致时返回 true。</returns>
    public bool CanHandle(YokiFrameProjectSettingsTarget target) =>
        target != null && string.Equals(target.Id, mTarget.Id, StringComparison.Ordinal);

    /// <summary>读取原始 Workbench JSON，不把领域草稿压平为 Runtime 字符串条目。</summary>
    /// <param name="target">当前草稿目标。</param>
    /// <param name="path">草稿绝对路径。</param>
    /// <returns>包含完整 JSON 原文的 owned-document。</returns>
    public YokiFrameProjectSettingsBackendDocument Read(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
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

    /// <summary>Workbench 草稿禁止使用标量 patch API，调用方必须使用 owned-document Gateway。</summary>
    /// <param name="document">不会被消费的草稿文档。</param>
    /// <param name="patches">不允许提交的标量 patch。</param>
    /// <returns>该方法始终抛出异常，不返回文本。</returns>
    public string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        throw new InvalidOperationException(mDisplayName + " draft must be written through the owned-document Gateway.");
    }

    /// <summary>返回当前 Workbench 草稿的稳定项目相对路径。</summary>
    /// <param name="target">当前草稿目标。</param>
    /// <returns>项目内稳定草稿路径。</returns>
    public string GetRelativePath(YokiFrameProjectSettingsTarget target) => mRelativePath;
}
