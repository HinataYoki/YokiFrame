using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.LogKit;

/// <summary>通过统一项目配置 Store 读取和保存 Unity LogKit Runtime/Editor 设置。</summary>
public sealed class LogKitRuntimeSettingsService
{
    private const string LOG_KIT = "LogKit";
    private const string GODOT_LOG_KIT = "log_kit";
    private readonly YokiFrameProjectSettingsStore mSettingsStore;

    /// <summary>创建绑定当前项目根的 LogKit 设置服务。</summary>
    /// <param name="projectRoot">当前 Workbench 项目根。</param>
    public LogKitRuntimeSettingsService(string projectRoot)
        : this(new YokiFrameProjectSettingsStore(projectRoot))
    {
    }

    /// <summary>创建复用统一项目配置 Store 的 LogKit 服务。</summary>
    /// <param name="settingsStore">项目级配置 Store。</param>
    public LogKitRuntimeSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    /// <summary>获取当前项目受控 Runtime Settings 绝对路径。</summary>
    public string SettingsPath => mSettingsStore.GetPath(YokiFrameProjectSettingsTarget.UnityRuntime);

    /// <summary>获取当前项目受控 Editor Settings 绝对路径。</summary>
    internal string EditorSettingsPath => mSettingsStore.GetPath(YokiFrameProjectSettingsTarget.UnityEditor);

    /// <summary>读取当前 Unity 项目的 LogKit 设置和组合文件 revision。</summary>
    /// <param name="engineId">当前 Unity engine 标识。</param>
    /// <returns>项目设置；文件缺失时返回 Core 默认值。</returns>
    public WorkbenchLogKitProjectSettings LoadUnitySettings(string engineId)
    {
        return LoadSettings(engineId, "Unity", YokiFrameProjectSettingsTarget.UnityRuntime, YokiFrameProjectSettingsTarget.UnityEditor);
    }

    /// <summary>读取当前 Godot 项目的 LogKit 设置；Runtime 来自 project.godot，编辑器字段来自 .yokiframe。</summary>
    /// <param name="engineId">当前 Godot engine 标识。</param>
    /// <returns>项目设置；两边都缺失时返回 Core 默认值。</returns>
    public WorkbenchLogKitProjectSettings LoadGodotSettings(string engineId)
    {
        return LoadSettings(engineId, "Godot", YokiFrameProjectSettingsTarget.GodotRuntime, YokiFrameProjectSettingsTarget.GodotEditor);
    }

    /// <summary>按宿主目标读取 Runtime 与 Editor 两份文档，并组合成一个 LogKit 设置。</summary>
    /// <param name="engineId">当前 engine 标识。</param>
    /// <param name="engine">宿主显示名称。</param>
    /// <param name="runtimeTarget">运行时配置目标。</param>
    /// <param name="editorTarget">编辑器配置目标。</param>
    /// <returns>项目设置；文件缺失时返回 Core 默认值。</returns>
    private WorkbenchLogKitProjectSettings LoadSettings(
        string engineId,
        string engine,
        YokiFrameProjectSettingsTarget runtimeTarget,
        YokiFrameProjectSettingsTarget editorTarget)
    {
        YokiFrameProjectSettingsSnapshot snapshot = mSettingsStore.Read(runtimeTarget, editorTarget);
        return CreateProjectSettings(engineId, engine, snapshot, ReadSettings(snapshot, runtimeTarget, editorTarget), runtimeTarget, editorTarget);
    }

    /// <summary>校验 revision 后通过统一 Store 批次保存完整 LogKit Runtime/Editor 设置。</summary>
    /// <param name="engineId">当前 Unity engine 标识。</param>
    /// <param name="settings">要保存的完整设置。</param>
    /// <param name="expectedFingerprint">页面加载时观察到的组合 revision。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>项目保存结果；Runtime 应用由 Dashboard 单独处理。</returns>
    public async Task<WorkbenchLogKitSettingsSaveResult> SaveUnitySettingsAsync(
        string engineId,
        WorkbenchLogKitSettings settings,
        string expectedFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFingerprint);
        if (!WorkbenchLogKitSettingsJson.TryValidate(settings, out string validationError))
        {
            throw new ArgumentException(validationError, nameof(settings));
        }

        YokiFrameProjectSettingsUpdate update = CreateSaveUpdate(
            expectedFingerprint,
            YokiFrameProjectSettingsTarget.UnityRuntime,
            YokiFrameProjectSettingsTarget.UnityEditor,
            settings);
        YokiFrameProjectSettingsWriteResult result = await mSettingsStore.WriteAsync(update, cancellationToken)
            .ConfigureAwait(false);
        WorkbenchLogKitProjectSettings projectSettings = CreateProjectSettings(
            engineId,
            "Unity",
            result.Snapshot,
            ReadSettings(result.Snapshot, YokiFrameProjectSettingsTarget.UnityRuntime, YokiFrameProjectSettingsTarget.UnityEditor),
            YokiFrameProjectSettingsTarget.UnityRuntime,
            YokiFrameProjectSettingsTarget.UnityEditor);
        return CreateSaveResult(result, projectSettings);
    }

    /// <summary>校验 Runtime 路径仍位于当前项目 Assets 内，保留既有测试和调用方契约。</summary>
    /// <param name="projectRoot">当前项目根。</param>
    /// <param name="relativePath">项目相对路径。</param>
    /// <returns>受路径约束的绝对路径。</returns>
    internal static string ResolveContainedPath(string projectRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Settings path must be project-relative.", nameof(relativePath));
        }

        string root = Path.GetFullPath(projectRoot);
        string assetsRoot = Path.GetFullPath(Path.Combine(root, "Assets"));
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        EnsureContained(candidate, root, nameof(relativePath));
        EnsureContained(candidate, assetsRoot, nameof(relativePath));
        return candidate;
    }

    /// <summary>
    /// 校验 revision 后保存 Godot LogKit 设置。运行时键写入 project.godot，编辑器键写入 .yokiframe。
    /// </summary>
    /// <param name="engineId">当前 Godot engine 标识。</param>
    /// <param name="settings">要保存的完整设置。</param>
    /// <param name="expectedFingerprint">页面加载时观察到的组合 revision。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>项目保存结果；Runtime 应用由 Dashboard 单独处理。</returns>
    public async Task<WorkbenchLogKitSettingsSaveResult> SaveGodotSettingsAsync(
        string engineId,
        WorkbenchLogKitSettings settings,
        string expectedFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFingerprint);
        if (!WorkbenchLogKitSettingsJson.TryValidate(settings, out string validationError))
        {
            throw new ArgumentException(validationError, nameof(settings));
        }

        YokiFrameProjectSettingsUpdate update = CreateSaveUpdate(
            expectedFingerprint,
            YokiFrameProjectSettingsTarget.GodotRuntime,
            YokiFrameProjectSettingsTarget.GodotEditor,
            settings,
            UseGodotRuntimeKeys(WorkbenchLogKitSettingsJson.CreateRuntimeStringValues(settings)),
            GODOT_LOG_KIT);
        YokiFrameProjectSettingsWriteResult result = await mSettingsStore.WriteAsync(update, cancellationToken)
            .ConfigureAwait(false);
        WorkbenchLogKitProjectSettings projectSettings = CreateProjectSettings(
            engineId,
            "Godot",
            result.Snapshot,
            ReadSettings(result.Snapshot, YokiFrameProjectSettingsTarget.GodotRuntime, YokiFrameProjectSettingsTarget.GodotEditor),
            YokiFrameProjectSettingsTarget.GodotRuntime,
            YokiFrameProjectSettingsTarget.GodotEditor);
        return CreateSaveResult(result, projectSettings);
    }

    /// <summary>按宿主目标创建 Runtime 与 Editor 的组合更新。</summary>
    /// <param name="expectedFingerprint">页面加载时观察到的组合 revision。</param>
    /// <param name="runtimeTarget">运行时配置目标。</param>
    /// <param name="editorTarget">编辑器配置目标。</param>
    /// <param name="settings">已校验的完整设置。</param>
    /// <param name="runtimeValues">运行时字符串值；缺省使用 Unity camelCase 键。</param>
    /// <returns>要求 revision 匹配的两文档更新。</returns>
    private static YokiFrameProjectSettingsUpdate CreateSaveUpdate(
        string expectedFingerprint,
        YokiFrameProjectSettingsTarget runtimeTarget,
        YokiFrameProjectSettingsTarget editorTarget,
        WorkbenchLogKitSettings settings,
        IReadOnlyList<KeyValuePair<string, string>>? runtimeValues = null,
        string runtimeOwner = LOG_KIT)
    {
        return YokiFrameProjectSettingsUpdate.RequireRevision(
            expectedFingerprint,
            YokiFrameProjectSettingsPatch.ReplaceOwner(
                runtimeTarget,
                runtimeOwner,
                CreateValues(runtimeValues ?? WorkbenchLogKitSettingsJson.CreateRuntimeStringValues(settings))),
            YokiFrameProjectSettingsPatch.ReplaceOwner(
                editorTarget,
                LOG_KIT,
                CreateValues(WorkbenchLogKitSettingsJson.CreateEditorStringValues(settings))));
    }

    /// <summary>把 Godot ProjectSettings 使用的 snake_case 键映射到统一保存值。</summary>
    /// <param name="values">Unity 风格的运行时键值。</param>
    /// <returns>Godot Runtime section 可直接写入的键值。</returns>
    private static IReadOnlyList<KeyValuePair<string, string>> UseGodotRuntimeKeys(
        IReadOnlyList<KeyValuePair<string, string>> values)
    {
        KeyValuePair<string, string>[] result = new KeyValuePair<string, string>[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            result[index] = new KeyValuePair<string, string>(
                ToGodotRuntimeKey(values[index].Key),
                values[index].Value);
        }

        return result;
    }

    /// <summary>把 LogKit Runtime 键转换为 Godot ProjectSettings 使用的键。</summary>
    /// <param name="key">Unity JSON 使用的 camelCase 键。</param>
    /// <returns>Godot Runtime section 使用的 snake_case 键。</returns>
    private static string ToGodotRuntimeKey(string key)
    {
        return GodotRuntimeSettingKeys.ToSnakeCase(key);
    }

    /// <summary>把 Store 条目转换为当前 LogKit 的强类型设置。</summary>
    private static WorkbenchLogKitSettings ReadSettings(
        YokiFrameProjectSettingsSnapshot snapshot,
        YokiFrameProjectSettingsTarget runtimeTarget,
        YokiFrameProjectSettingsTarget editorTarget)
    {
        string runtimeOwner = runtimeTarget == YokiFrameProjectSettingsTarget.GodotRuntime ? GODOT_LOG_KIT : LOG_KIT;
        IReadOnlyDictionary<string, string> runtime = NormalizeRuntimeKeys(snapshot
            .GetDocument(runtimeTarget).GetValues(runtimeOwner));
        IReadOnlyDictionary<string, string> editor = snapshot
            .GetDocument(editorTarget).GetValues(LOG_KIT);
        WorkbenchLogKitSettings defaults = WorkbenchLogKitSettings.CreateDefault();
        WorkbenchLogKitSettings settings = defaults with
        {
            Enabled = ReadBoolean(runtime, "enabled", defaults.Enabled),
            MinimumLevel = WorkbenchLogKitSettingsJson.NormalizeLevel(ReadString(runtime, "minimumLevel", defaults.MinimumLevel))
                ?? ReadString(runtime, "minimumLevel", defaults.MinimumLevel),
            SaveLogInPlayer = ReadBoolean(runtime, "saveLogInPlayer", defaults.SaveLogInPlayer),
            EnableIMGUIInPlayer = ReadBoolean(runtime, "enableIMGUIInPlayer", defaults.EnableIMGUIInPlayer),
            EnableEncryption = ReadBoolean(runtime, "enableEncryption", defaults.EnableEncryption),
            MaxQueueSize = ReadInteger(runtime, "maxQueueSize", defaults.MaxQueueSize),
            MaxSameLogCount = ReadInteger(runtime, "maxSameLogCount", defaults.MaxSameLogCount),
            MaxRetentionDays = ReadInteger(runtime, "maxRetentionDays", defaults.MaxRetentionDays),
            MaxFileSizeMB = ReadInteger(runtime, "maxFileSizeMB", defaults.MaxFileSizeMB),
            ImguiMaxLogCount = ReadInteger(runtime, "imguiMaxLogCount", defaults.ImguiMaxLogCount),
            LogDirectory = ReadString(runtime, "logDirectory", defaults.LogDirectory),
            PlayerFileName = ReadString(runtime, "playerFileName", defaults.PlayerFileName),
            SaveLogInEditor = ReadBoolean(editor, "saveLogInEditor", defaults.SaveLogInEditor),
            EditorFileName = ReadString(editor, "editorFileName", defaults.EditorFileName)
        };
        if (!WorkbenchLogKitSettingsJson.TryValidate(settings, out string errorMessage))
        {
            throw new InvalidDataException("Persisted LogKit settings are invalid: " + errorMessage);
        }

        return settings;
    }

    /// <summary>创建 Workbench LogKit 项目 read model。</summary>
    private static WorkbenchLogKitProjectSettings CreateProjectSettings(
        string engineId,
        string engine,
        YokiFrameProjectSettingsSnapshot snapshot,
        WorkbenchLogKitSettings settings,
        YokiFrameProjectSettingsTarget runtimeTarget,
        YokiFrameProjectSettingsTarget editorTarget)
    {
        YokiFrameProjectSettingsDocument runtime = snapshot.GetDocument(runtimeTarget);
        bool exists = runtime.Exists || snapshot.GetDocument(editorTarget).Exists;
        return new WorkbenchLogKitProjectSettings(
            engineId,
            engine,
            true,
            exists,
            runtime.Path,
            snapshot.Revision,
            settings,
            exists ? "Project runtime settings loaded." : "Project runtime settings file is missing; Core defaults are active.");
    }

    /// <summary>把统一写入结果转换为 LogKit 页面使用的保存结果。</summary>
    private static WorkbenchLogKitSettingsSaveResult CreateSaveResult(
        YokiFrameProjectSettingsWriteResult result,
        WorkbenchLogKitProjectSettings projectSettings)
    {
        return new WorkbenchLogKitSettingsSaveResult(
            result.Saved,
            false,
            result.ConflictDetected,
            projectSettings,
            null,
            result.ConflictDetected ? "Runtime settings changed after they were loaded. Reload before saving." : string.Empty);
    }

    /// <summary>把 Godot snake_case Runtime 键归一成 Workbench 读取使用的 camelCase 键。</summary>
    private static IReadOnlyDictionary<string, string> NormalizeRuntimeKeys(IReadOnlyDictionary<string, string> values)
    {
        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in values)
        {
            normalized[FromGodotRuntimeKey(pair.Key)] = pair.Value;
        }

        return normalized;
    }

    /// <summary>把 Godot Runtime 键还原为 Workbench LogKit 模型键；没有下划线的键保持原样。</summary>
    private static string FromGodotRuntimeKey(string key)
    {
        return GodotRuntimeSettingKeys.ToCamelCase(key);
    }

    /// <summary>把 LogKit 的字符串键值转换为统一 Store patch 参数。</summary>
    private static YokiFrameProjectSettingValue[] CreateValues(IReadOnlyList<KeyValuePair<string, string>> values)
    {
        return values.Select(static pair => new YokiFrameProjectSettingValue(pair.Key, pair.Value)).ToArray();
    }

    /// <summary>读取字符串设置，缺失时回退到代码默认值。</summary>
    private static string ReadString(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out string? value) ? value : fallback;
    }

    /// <summary>严格读取布尔设置，缺失时回退默认值，非法值明确失败。</summary>
    private static bool ReadBoolean(IReadOnlyDictionary<string, string> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out string? value)) return fallback;
        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw new InvalidDataException("LogKit " + key + " must be a boolean.");
    }

    /// <summary>严格读取整数设置，缺失时回退默认值，非法值明确失败。</summary>
    private static int ReadInteger(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        if (!values.TryGetValue(key, out string? value)) return fallback;
        return int.TryParse(value, out int parsed)
            ? parsed
            : throw new InvalidDataException("LogKit " + key + " must be an integer.");
    }

    /// <summary>校验路径包含关系，阻止配置写入当前项目之外。</summary>
    private static void EnsureContained(string candidate, string root, string parameterName)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, comparison))
        {
            throw new ArgumentException("Settings path must remain inside the current project.", parameterName);
        }
    }
}
