namespace YokiFrame.Tooling.Application.Services.Settings;

/// <summary>标识配置属于 Runtime、团队共享 Editor Project 还是用户本地 Editor User 域。</summary>
public enum YokiFrameProjectSettingsScope
{
    Runtime,
    EditorProject,
    EditorUser
}

/// <summary>
/// 标识一个项目配置文档的引擎、配置域和文档用途。
/// 内置目标只是静态实例，未来引擎可以创建自己的目标而不修改 Store 核心代码。
/// </summary>
public sealed record YokiFrameProjectSettingsTarget
{
    /// <summary>创建一个自定义项目配置目标。</summary>
    /// <param name="engineId">稳定引擎标识。</param>
    /// <param name="scope">配置域。</param>
    /// <param name="documentId">同一域内的文档标识。</param>
    public YokiFrameProjectSettingsTarget(
        string engineId,
        YokiFrameProjectSettingsScope scope,
        string documentId = "settings")
    {
        YokiFrameProjectSettingsStore.ValidateIdentifier(engineId, nameof(engineId));
        YokiFrameProjectSettingsStore.ValidateIdentifier(documentId, nameof(documentId));
        EngineId = engineId;
        Scope = scope;
        DocumentId = documentId;
    }

    /// <summary>Unity Runtime Settings 文档。</summary>
    public static YokiFrameProjectSettingsTarget UnityRuntime { get; } =
        new("unity", YokiFrameProjectSettingsScope.Runtime);

    /// <summary>Unity 团队共享 Editor Project Settings 文档。</summary>
    public static YokiFrameProjectSettingsTarget UnityEditor { get; } =
        new("unity", YokiFrameProjectSettingsScope.EditorProject);

    /// <summary>Unity 用户本地 Editor Settings 文档。</summary>
    public static YokiFrameProjectSettingsTarget UnityEditorUser { get; } =
        new("unity", YokiFrameProjectSettingsScope.EditorUser);

    /// <summary>Godot ProjectSettings Runtime 文档。</summary>
    public static YokiFrameProjectSettingsTarget GodotRuntime { get; } =
        new("godot", YokiFrameProjectSettingsScope.Runtime, "project");

    /// <summary>Godot 团队共享 Editor Project Settings 文档。</summary>
    public static YokiFrameProjectSettingsTarget GodotEditor { get; } =
        new("godot", YokiFrameProjectSettingsScope.EditorProject);

    /// <summary>Godot 用户本地 Editor Settings 文档。</summary>
    public static YokiFrameProjectSettingsTarget GodotEditorUser { get; } =
        new("godot", YokiFrameProjectSettingsScope.EditorUser);

    /// <summary>TableKit 独占的 Workbench 草稿文档，不属于 Runtime Settings。</summary>
    public static YokiFrameProjectSettingsTarget TableKitDraft { get; } =
        new("shared", YokiFrameProjectSettingsScope.EditorProject, "tablekit");

    /// <summary>LocalizationKit 独占的 Workbench 草稿文档，不属于 Runtime Settings。</summary>
    public static YokiFrameProjectSettingsTarget LocalizationKitDraft { get; } =
        new("shared", YokiFrameProjectSettingsScope.EditorProject, "localizationkit");

    /// <summary>获取稳定引擎标识。</summary>
    public string EngineId { get; }

    /// <summary>获取配置域。</summary>
    public YokiFrameProjectSettingsScope Scope { get; }

    /// <summary>获取同一域内的文档用途标识。</summary>
    public string DocumentId { get; }

    /// <summary>获取用于注册表和诊断的稳定目标键。</summary>
    public string Id => EngineId + ":" + Scope + ":" + DocumentId;
}

/// <summary>定义一个引擎项目配置后端；Store 负责并发、revision 和原子提交。</summary>
public interface IYokiFrameProjectSettingsBackend
{
    /// <summary>获取后端拥有的引擎标识。</summary>
    string EngineId { get; }

    /// <summary>判断后端是否支持指定配置目标。</summary>
    /// <param name="target">待判断目标。</param>
    /// <returns>支持该目标时返回 true。</returns>
    bool CanHandle(YokiFrameProjectSettingsTarget target);

    /// <summary>读取并解析一个物理配置文档。</summary>
    /// <param name="target">目标标识。</param>
    /// <param name="path">已由 Store 解析并守卫的绝对路径。</param>
    /// <returns>后端格式解析结果。</returns>
    YokiFrameProjectSettingsBackendDocument Read(
        YokiFrameProjectSettingsTarget target,
        string path);

    /// <summary>把 Store 应用 patch 后的文档序列化为完整文本。</summary>
    /// <param name="document">本次读取的后端文档。</param>
    /// <param name="patches">针对该目标的 owner patch。</param>
    /// <returns>待原子提交的完整文本。</returns>
    string Serialize(
        YokiFrameProjectSettingsBackendDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches);

    /// <summary>根据目标计算物理配置项目相对路径。</summary>
    /// <param name="target">目标标识。</param>
    /// <returns>项目内相对路径。</returns>
    string GetRelativePath(YokiFrameProjectSettingsTarget target);
}

/// <summary>
/// 标记允许通过 owned-document Gateway 直接读写完整文本的后端。
/// 普通 Runtime/Editor Settings 后端不得实现该接口，避免绕过结构化 patch 校验。
/// </summary>
public interface IYokiFrameProjectOwnedDocumentBackend : IYokiFrameProjectSettingsBackend
{
}

/// <summary>保存引擎后端一次读取的原文和结构化设置投影。</summary>
public sealed class YokiFrameProjectSettingsBackendDocument
{
    /// <summary>创建后端读取结果。</summary>
    /// <param name="target">目标标识。</param>
    /// <param name="path">物理绝对路径。</param>
    /// <param name="exists">正式文件是否存在。</param>
    /// <param name="originalText">读取到的完整原文。</param>
    /// <param name="settings">后端投影出的 owner/key 字符串条目。</param>
    /// <param name="fingerprint">后端读取的原始字节指纹；缺省时由 Store 根据原文计算。</param>
    public YokiFrameProjectSettingsBackendDocument(
        YokiFrameProjectSettingsTarget target,
        string path,
        bool exists,
        string originalText,
        IReadOnlyList<YokiFrameProjectSetting> settings,
        string fingerprint = "")
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        OriginalText = originalText ?? string.Empty;
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Exists = exists;
        Fingerprint = fingerprint;
    }

    /// <summary>获取目标标识。</summary>
    public YokiFrameProjectSettingsTarget Target { get; }

    /// <summary>获取物理绝对路径。</summary>
    public string Path { get; }

    /// <summary>获取正式文件是否存在。</summary>
    public bool Exists { get; }

    /// <summary>获取后端原始文档文本。</summary>
    public string OriginalText { get; }

    /// <summary>获取结构化设置条目。</summary>
    public IReadOnlyList<YokiFrameProjectSetting> Settings { get; }

    /// <summary>获取后端按原始字节计算的内容指纹。</summary>
    public string Fingerprint { get; }
}

/// <summary>保存复杂 Workbench owned-document 的读取快照。</summary>
public sealed record YokiFrameProjectOwnedDocumentSnapshot(
    YokiFrameProjectSettingsTarget Target,
    string Path,
    bool Exists,
    string Fingerprint,
    string Content);

/// <summary>返回 owned-document 原子提交的状态和最新快照。</summary>
public sealed record YokiFrameProjectOwnedDocumentWriteResult(
    bool Saved,
    bool ConflictDetected,
    YokiFrameProjectOwnedDocumentSnapshot Snapshot);

/// <summary>标识统一写入采用最新值合并还是要求指定 revision。</summary>
public enum YokiFrameProjectSettingsWriteMode
{
    MergeLatest,
    RequireRevision
}

/// <summary>标识一次 patch 替换完整 owner 还是 owner 下的指定键。</summary>
public enum YokiFrameProjectSettingsPatchMode
{
    ReplaceOwner,
    ReplaceKeys
}

/// <summary>保存统一配置中的一个结构化字符串条目。</summary>
/// <param name="Owner">条目所有者；Unity 对应 kit，Godot 对应路径首段。</param>
/// <param name="Key">所有者范围内的设置键。</param>
/// <param name="Value">设置字符串值。</param>
public sealed record YokiFrameProjectSetting(string Owner, string Key, string Value);

/// <summary>保存一次 patch 要写入的键值。</summary>
/// <param name="Key">所有者范围内的设置键。</param>
/// <param name="Value">设置字符串值。</param>
public sealed record YokiFrameProjectSettingValue(string Key, string Value);

/// <summary>保存一个物理配置目标的结构化快照。</summary>
public sealed class YokiFrameProjectSettingsDocument
{
    /// <summary>创建已完成路径和格式校验的配置文档。</summary>
    internal YokiFrameProjectSettingsDocument(
        YokiFrameProjectSettingsTarget target,
        string path,
        bool exists,
        string fingerprint,
        IReadOnlyList<YokiFrameProjectSetting> settings)
    {
        Target = target;
        Path = path;
        Exists = exists;
        Fingerprint = fingerprint;
        Settings = settings;
    }

    /// <summary>获取物理配置目标。</summary>
    public YokiFrameProjectSettingsTarget Target { get; }

    /// <summary>获取绑定当前项目根的配置绝对路径。</summary>
    public string Path { get; }

    /// <summary>获取读取时正式文件是否存在。</summary>
    public bool Exists { get; }

    /// <summary>获取正式文件内容指纹；文件缺失时为 missing。</summary>
    public string Fingerprint { get; }

    /// <summary>获取已完成格式校验的全部稀疏条目。</summary>
    public IReadOnlyList<YokiFrameProjectSetting> Settings { get; }

    /// <summary>按最后条目生效规则读取指定 owner 的完整字符串字典。</summary>
    /// <param name="owner">Unity kit 或 Godot 路径首段。</param>
    /// <returns>只包含指定 owner 的键值字典。</returns>
    public IReadOnlyDictionary<string, string> GetValues(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (YokiFrameProjectSetting setting in Settings)
        {
            if (string.Equals(setting.Owner, owner, StringComparison.Ordinal))
            {
                values[setting.Key] = setting.Value;
            }
        }

        return values;
    }
}

/// <summary>保存一次一致读取所覆盖的配置文档及组合 revision。</summary>
public sealed class YokiFrameProjectSettingsSnapshot
{
    private readonly IReadOnlyDictionary<YokiFrameProjectSettingsTarget, YokiFrameProjectSettingsDocument> mDocuments;

    /// <summary>创建按目标索引的一致配置快照。</summary>
    internal YokiFrameProjectSettingsSnapshot(
        IReadOnlyDictionary<YokiFrameProjectSettingsTarget, YokiFrameProjectSettingsDocument> documents,
        string revision)
    {
        mDocuments = documents;
        Revision = revision;
    }

    /// <summary>获取快照覆盖目标的组合 revision。</summary>
    public string Revision { get; }

    /// <summary>获取指定目标文档；未包含该目标时明确失败。</summary>
    /// <param name="target">待读取目标。</param>
    /// <returns>目标的结构化文档。</returns>
    public YokiFrameProjectSettingsDocument GetDocument(YokiFrameProjectSettingsTarget target)
    {
        return mDocuments.TryGetValue(target, out YokiFrameProjectSettingsDocument? document)
            ? document
            : throw new ArgumentOutOfRangeException(nameof(target), target, "Settings target was not included in this snapshot.");
    }
}

/// <summary>描述一个 owner 对单个物理配置目标的结构化变更。</summary>
public sealed class YokiFrameProjectSettingsPatch
{
    private YokiFrameProjectSettingsPatch(
        YokiFrameProjectSettingsTarget target,
        string owner,
        YokiFrameProjectSettingsPatchMode mode,
        IReadOnlySet<string> replacedKeys,
        IReadOnlyList<YokiFrameProjectSettingValue> values)
    {
        Target = target;
        Owner = owner;
        Mode = mode;
        ReplacedKeys = replacedKeys;
        Values = values;
    }

    /// <summary>获取变更目标。</summary>
    public YokiFrameProjectSettingsTarget Target { get; }

    /// <summary>获取变更所有者。</summary>
    public string Owner { get; }

    /// <summary>获取替换范围。</summary>
    public YokiFrameProjectSettingsPatchMode Mode { get; }

    /// <summary>获取 ReplaceKeys 模式拥有的全部键。</summary>
    public IReadOnlySet<string> ReplacedKeys { get; }

    /// <summary>获取待写入的稳定键值序列。</summary>
    public IReadOnlyList<YokiFrameProjectSettingValue> Values { get; }

    /// <summary>创建替换指定 owner 全部条目的 patch。</summary>
    public static YokiFrameProjectSettingsPatch ReplaceOwner(
        YokiFrameProjectSettingsTarget target,
        string owner,
        params YokiFrameProjectSettingValue[] values)
    {
        Validate(owner, values);
        return new YokiFrameProjectSettingsPatch(
            target, owner, YokiFrameProjectSettingsPatchMode.ReplaceOwner,
            new HashSet<string>(StringComparer.Ordinal), values.ToArray());
    }

    /// <summary>创建只替换指定 owner 已声明键集合的 patch。</summary>
    public static YokiFrameProjectSettingsPatch ReplaceKeys(
        YokiFrameProjectSettingsTarget target,
        string owner,
        IEnumerable<string> replacedKeys,
        params YokiFrameProjectSettingValue[] values)
    {
        Validate(owner, values);
        HashSet<string> keys = new(replacedKeys ?? throw new ArgumentNullException(nameof(replacedKeys)), StringComparer.Ordinal);
        if (keys.Count == 0) throw new ArgumentException("At least one replaced key is required.", nameof(replacedKeys));
        foreach (YokiFrameProjectSettingValue value in values)
        {
            if (!keys.Contains(value.Key)) throw new ArgumentException("Patch values must belong to replaced keys.", nameof(values));
        }

        return new YokiFrameProjectSettingsPatch(
            target, owner, YokiFrameProjectSettingsPatchMode.ReplaceKeys, keys, values.ToArray());
    }

    /// <summary>判断现有条目是否归当前 patch 所有。</summary>
    internal bool Owns(string owner, string key)
    {
        if (!string.Equals(Owner, owner, StringComparison.Ordinal)) return false;
        return Mode == YokiFrameProjectSettingsPatchMode.ReplaceOwner || ReplacedKeys.Contains(key);
    }

    /// <summary>校验 owner、键和值均可安全进入共享配置格式。</summary>
    private static void Validate(string owner, IReadOnlyList<YokiFrameProjectSettingValue> values)
    {
        YokiFrameProjectSettingsStore.ValidateIdentifier(owner, nameof(owner));
        ArgumentNullException.ThrowIfNull(values);
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (YokiFrameProjectSettingValue value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            YokiFrameProjectSettingsStore.ValidateIdentifier(value.Key, nameof(values));
            if (!keys.Add(value.Key)) throw new ArgumentException("Patch keys must be unique.", nameof(values));
        }
    }
}

/// <summary>描述统一写入入口的一次批量更新。</summary>
public sealed class YokiFrameProjectSettingsUpdate
{
    private YokiFrameProjectSettingsUpdate(
        YokiFrameProjectSettingsWriteMode mode,
        string expectedRevision,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        Mode = mode;
        ExpectedRevision = expectedRevision;
        Patches = patches;
    }

    /// <summary>获取并发策略。</summary>
    public YokiFrameProjectSettingsWriteMode Mode { get; }

    /// <summary>获取 RequireRevision 模式要求的 revision。</summary>
    public string ExpectedRevision { get; }

    /// <summary>获取同一事务提交的全部 patch。</summary>
    public IReadOnlyList<YokiFrameProjectSettingsPatch> Patches { get; }

    /// <summary>创建在项目锁内重读最新配置后合并的更新。</summary>
    public static YokiFrameProjectSettingsUpdate MergeLatest(params YokiFrameProjectSettingsPatch[] patches)
    {
        return Create(YokiFrameProjectSettingsWriteMode.MergeLatest, string.Empty, patches);
    }

    /// <summary>创建仅在组合 revision 未变化时提交的更新。</summary>
    public static YokiFrameProjectSettingsUpdate RequireRevision(
        string expectedRevision,
        params YokiFrameProjectSettingsPatch[] patches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRevision);
        return Create(YokiFrameProjectSettingsWriteMode.RequireRevision, expectedRevision, patches);
    }

    /// <summary>校验并复制调用方 patch，避免写入期间集合变化。</summary>
    private static YokiFrameProjectSettingsUpdate Create(
        YokiFrameProjectSettingsWriteMode mode,
        string expectedRevision,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        ArgumentNullException.ThrowIfNull(patches);
        if (patches.Count == 0) throw new ArgumentException("At least one settings patch is required.", nameof(patches));
        if (patches.Any(static patch => patch == null)) throw new ArgumentException("Settings patches cannot contain null.", nameof(patches));
        return new YokiFrameProjectSettingsUpdate(mode, expectedRevision, patches.ToArray());
    }
}

/// <summary>返回统一写入的提交状态和最新配置快照。</summary>
public sealed record YokiFrameProjectSettingsWriteResult(
    bool Saved,
    bool ConflictDetected,
    YokiFrameProjectSettingsSnapshot Snapshot);
