using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.LogKit;

/// <summary>通过统一项目配置 Store 读取和保存 Unity LogKit Runtime/Editor 设置。</summary>
public sealed class LogKitRuntimeSettingsService
{
    private const string LOG_KIT = "LogKit";
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
        YokiFrameProjectSettingsSnapshot snapshot = mSettingsStore.Read(
            YokiFrameProjectSettingsTarget.UnityRuntime,
            YokiFrameProjectSettingsTarget.UnityEditor);
        return CreateProjectSettings(engineId, snapshot, ReadSettings(snapshot));
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

        YokiFrameProjectSettingsUpdate update = YokiFrameProjectSettingsUpdate.RequireRevision(
            expectedFingerprint,
            YokiFrameProjectSettingsPatch.ReplaceOwner(
                YokiFrameProjectSettingsTarget.UnityRuntime,
                LOG_KIT,
                CreateValues(WorkbenchLogKitSettingsJson.CreateRuntimeStringValues(settings))),
            YokiFrameProjectSettingsPatch.ReplaceOwner(
                YokiFrameProjectSettingsTarget.UnityEditor,
                LOG_KIT,
                CreateValues(WorkbenchLogKitSettingsJson.CreateEditorStringValues(settings))));
        YokiFrameProjectSettingsWriteResult result = await mSettingsStore.WriteAsync(update, cancellationToken)
            .ConfigureAwait(false);
        WorkbenchLogKitProjectSettings projectSettings = CreateProjectSettings(
            engineId, result.Snapshot, ReadSettings(result.Snapshot));
        return new WorkbenchLogKitSettingsSaveResult(
            result.Saved,
            false,
            result.ConflictDetected,
            projectSettings,
            null,
            result.ConflictDetected ? "Runtime settings changed after they were loaded. Reload before saving." : string.Empty);
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

    /// <summary>把 Store 条目转换为当前 LogKit 的强类型设置。</summary>
    private static WorkbenchLogKitSettings ReadSettings(YokiFrameProjectSettingsSnapshot snapshot)
    {
        IReadOnlyDictionary<string, string> runtime = snapshot
            .GetDocument(YokiFrameProjectSettingsTarget.UnityRuntime).GetValues(LOG_KIT);
        IReadOnlyDictionary<string, string> editor = snapshot
            .GetDocument(YokiFrameProjectSettingsTarget.UnityEditor).GetValues(LOG_KIT);
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
    private WorkbenchLogKitProjectSettings CreateProjectSettings(
        string engineId,
        YokiFrameProjectSettingsSnapshot snapshot,
        WorkbenchLogKitSettings settings)
    {
        YokiFrameProjectSettingsDocument runtime = snapshot.GetDocument(YokiFrameProjectSettingsTarget.UnityRuntime);
        bool exists = runtime.Exists || snapshot.GetDocument(YokiFrameProjectSettingsTarget.UnityEditor).Exists;
        return new WorkbenchLogKitProjectSettings(
            engineId,
            "Unity",
            true,
            exists,
            runtime.Path,
            snapshot.Revision,
            settings,
            exists ? "Project runtime settings loaded." : "Project runtime settings file is missing; Core defaults are active.");
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
