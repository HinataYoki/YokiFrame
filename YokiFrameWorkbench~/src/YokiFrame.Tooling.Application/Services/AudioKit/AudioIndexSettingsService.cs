using System.Text.Json;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.AudioKit;

/// <summary>通过统一项目配置 Store 保存项目级 AudioKit 索引设置。</summary>
public sealed class AudioIndexSettingsService
{
    internal const string LEGACY_SETTINGS_RELATIVE_PATH =
        "ProjectSettings/Packages/com.hinatayoki.yokiframe/audio-index-settings.json";
    private const string AUDIO_KIT = "AudioKit";
    private const string SCAN_FOLDER_KEY = "index.scanFolder";
    private const string OUTPUT_PATH_KEY = "index.outputPath";
    private const string MANIFEST_PATH_KEY = "index.manifestPath";
    private const string NAMESPACE_KEY = "index.namespaceName";
    private const string CLASS_NAME_KEY = "index.className";
    private const string START_ID_KEY = "index.startId";
    private const int MAX_LEGACY_SETTINGS_BYTES = 1024 * 1024;
    private static readonly string[] sOwnedKeys =
    {
        SCAN_FOLDER_KEY, OUTPUT_PATH_KEY, MANIFEST_PATH_KEY,
        NAMESPACE_KEY, CLASS_NAME_KEY, START_ID_KEY
    };
    private readonly string mProjectRoot;
    private readonly string mLegacySettingsPath;
    private readonly YokiFrameProjectSettingsStore mSettingsStore;

    /// <summary>创建绑定一个规范化项目根的设置服务。</summary>
    /// <param name="projectRoot">当前 Workbench 项目根。</param>
    public AudioIndexSettingsService(string projectRoot)
        : this(new YokiFrameProjectSettingsStore(projectRoot))
    {
    }

    /// <summary>创建复用统一项目配置 Store 的 AudioKit 设置服务。</summary>
    /// <param name="settingsStore">项目级配置 Store。</param>
    public AudioIndexSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        mProjectRoot = settingsStore.ProjectRoot;
        mLegacySettingsPath = ResolveProjectSettingsPath(mProjectRoot, LEGACY_SETTINGS_RELATIVE_PATH);
    }

    /// <summary>获取 YokiFrame 统一 Editor Settings 绝对路径。</summary>
    public string SettingsPath => mSettingsStore.GetPath(YokiFrameProjectSettingsTarget.UnityEditor);

    /// <summary>获取待自动迁移的历史独立配置路径。</summary>
    internal string LegacySettingsPath => mLegacySettingsPath;

    /// <summary>读取 AudioKit 条目；首次读取时通过统一 Store 迁移历史独立配置。</summary>
    /// <returns>已校验的项目配置。</returns>
    public AudioIndexSettings Load()
    {
        YokiFrameProjectSettingsSnapshot snapshot = mSettingsStore.Read(YokiFrameProjectSettingsTarget.UnityEditor);
        IReadOnlyDictionary<string, string> values = snapshot
            .GetDocument(YokiFrameProjectSettingsTarget.UnityEditor).GetValues(AUDIO_KIT);
        if (TryCreateSettings(values, out AudioIndexSettings settings))
        {
            DeleteLegacySettings();
            return settings;
        }

        if (!File.Exists(mLegacySettingsPath)) return AudioIndexSettings.CreateDefault();
        settings = ReadLegacySettings();
        YokiFrameProjectSettingsWriteResult result = mSettingsStore.WriteAsync(
                YokiFrameProjectSettingsUpdate.MergeLatest(CreatePatch(settings)), CancellationToken.None)
            .GetAwaiter().GetResult();
        if (!result.Saved) throw new IOException("AudioKit Editor Settings changed during legacy migration.");
        DeleteLegacySettings();
        return settings;
    }

    /// <summary>校验并通过统一 Store 异步保存到 Editor Settings。</summary>
    /// <param name="settings">要持久化的完整配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>统一配置提交完成任务。</returns>
    public async Task SaveAsync(AudioIndexSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSettings(settings);
        YokiFrameProjectSettingsWriteResult result = await mSettingsStore.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(CreatePatch(settings)), cancellationToken).ConfigureAwait(false);
        if (!result.Saved) throw new IOException("AudioKit Editor Settings changed while the update was being committed.");
        DeleteLegacySettings();
    }

    /// <summary>按统一“最后条目生效”语义创建 AudioKit 索引设置。</summary>
    private bool TryCreateSettings(
        IReadOnlyDictionary<string, string> values,
        out AudioIndexSettings settings)
    {
        if (!sOwnedKeys.Any(values.ContainsKey))
        {
            settings = AudioIndexSettings.CreateDefault();
            return false;
        }

        AudioIndexSettings defaults = AudioIndexSettings.CreateDefault();
        settings = new AudioIndexSettings(
            ReadValue(values, SCAN_FOLDER_KEY, defaults.ScanFolder),
            ReadValue(values, OUTPUT_PATH_KEY, defaults.OutputPath),
            ReadValue(values, MANIFEST_PATH_KEY, defaults.ManifestPath),
            ReadValue(values, NAMESPACE_KEY, defaults.NamespaceName),
            ReadValue(values, CLASS_NAME_KEY, defaults.ClassName),
            ReadStartId(values, defaults.StartId));
        ValidateSettings(settings);
        return true;
    }

    /// <summary>创建只替换 AudioKit 索引字段的统一 Store patch。</summary>
    private static YokiFrameProjectSettingsPatch CreatePatch(AudioIndexSettings settings)
    {
        return YokiFrameProjectSettingsPatch.ReplaceKeys(
            YokiFrameProjectSettingsTarget.UnityEditor,
            AUDIO_KIT,
            sOwnedKeys,
            new YokiFrameProjectSettingValue(SCAN_FOLDER_KEY, settings.ScanFolder.Trim()),
            new YokiFrameProjectSettingValue(OUTPUT_PATH_KEY, settings.OutputPath.Trim()),
            new YokiFrameProjectSettingValue(MANIFEST_PATH_KEY, settings.ManifestPath.Trim()),
            new YokiFrameProjectSettingValue(NAMESPACE_KEY, settings.NamespaceName.Trim()),
            new YokiFrameProjectSettingValue(CLASS_NAME_KEY, settings.ClassName.Trim()),
            new YokiFrameProjectSettingValue(START_ID_KEY, settings.StartId.ToString()));
    }

    /// <summary>读取历史独立 JSON，供一次性自动迁移。</summary>
    private AudioIndexSettings ReadLegacySettings()
    {
        byte[] bytes = ReadBoundedLegacyFile(mLegacySettingsPath);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            AudioIndexSettings settings = new(
                ReadLegacyString(root, "scanFolder"),
                ReadLegacyString(root, "outputPath"),
                ReadLegacyString(root, "manifestPath"),
                ReadLegacyString(root, "namespaceName"),
                ReadLegacyString(root, "className"),
                ReadLegacyInteger(root, "startId"));
            ValidateSettings(settings);
            return settings;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Legacy AudioKit index settings JSON is invalid.", exception);
        }
    }

    /// <summary>校验项目路径、代码标识和起始 ID。</summary>
    private void ValidateSettings(AudioIndexSettings settings)
    {
        _ = ResolveInsideProject(mProjectRoot, RequireText(settings.ScanFolder, SCAN_FOLDER_KEY));
        _ = ResolveInsideProject(mProjectRoot, RequireText(settings.OutputPath, OUTPUT_PATH_KEY));
        _ = ResolveInsideProject(mProjectRoot, RequireText(settings.ManifestPath, MANIFEST_PATH_KEY));
        ValidateNamespace(RequireText(settings.NamespaceName, NAMESPACE_KEY));
        ValidateIdentifier(RequireText(settings.ClassName, CLASS_NAME_KEY), CLASS_NAME_KEY);
        if (settings.StartId <= 0) throw new InvalidDataException("AudioKit index startId must be positive.");
    }

    /// <summary>读取历史配置必需字符串字段。</summary>
    private static string ReadLegacyString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Legacy AudioKit index settings are missing " + name + ".");
        }

        return value.GetString() ?? string.Empty;
    }

    /// <summary>读取历史配置必需整数值。</summary>
    private static int ReadLegacyInteger(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int result))
        {
            throw new InvalidDataException("Legacy AudioKit index settings are missing " + name + ".");
        }

        return result;
    }

    /// <summary>读取稀疏配置值，不存在时返回默认值。</summary>
    private static string ReadValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback)
    {
        return values.TryGetValue(key, out string? value) ? value : fallback;
    }

    /// <summary>读取起始 ID 并拒绝损坏整数。</summary>
    private static int ReadStartId(IReadOnlyDictionary<string, string> values, int fallback)
    {
        if (!values.TryGetValue(START_ID_KEY, out string? value)) return fallback;
        return int.TryParse(value, out int startId)
            ? startId
            : throw new InvalidDataException("AudioKit index.startId must be an integer.");
    }

    /// <summary>读取有界历史配置文件，拒绝异常大输入。</summary>
    private static byte[] ReadBoundedLegacyFile(string path)
    {
        FileInfo info = new(path);
        if (info.Length > MAX_LEGACY_SETTINGS_BYTES) throw new InvalidDataException("Legacy AudioKit Settings exceed 1 MiB.");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > MAX_LEGACY_SETTINGS_BYTES) throw new InvalidDataException("Legacy AudioKit Settings exceed 1 MiB.");
        return bytes;
    }

    /// <summary>删除已经被统一 Store 接管的历史独立配置。</summary>
    private void DeleteLegacySettings()
    {
        if (File.Exists(mLegacySettingsPath)) File.Delete(mLegacySettingsPath);
    }

    /// <summary>解析固定路径并确认结果仍位于当前项目 ProjectSettings。</summary>
    private static string ResolveProjectSettingsPath(string projectRoot, string relativePath)
    {
        string projectSettingsRoot = Path.GetFullPath(Path.Combine(projectRoot, "ProjectSettings"));
        string candidate = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        EnsureContained(projectSettingsRoot, candidate, "Editor Settings");
        return candidate;
    }

    /// <summary>解析任意项目相对业务路径并拒绝跨项目结果。</summary>
    private static string ResolveInsideProject(string projectRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("AudioKit index paths must be project-relative.");
        string candidate = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        EnsureContained(projectRoot, candidate, "AudioKit index path");
        return candidate;
    }

    /// <summary>使用相对路径语义验证候选路径没有逃逸。</summary>
    private static void EnsureContained(string root, string candidate, string name)
    {
        string relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException(name + " must stay inside its project boundary.");
        }
    }

    /// <summary>要求设置文本非空并返回裁剪值。</summary>
    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("AudioKit " + name + " cannot be empty.");
        return value.Trim();
    }

    /// <summary>验证点分隔 C# 命名空间的每个标识符。</summary>
    private static void ValidateNamespace(string value)
    {
        string[] segments = value.Split('.');
        if (segments.Length == 0 || segments.Any(static segment => !IsIdentifier(segment)))
        {
            throw new InvalidDataException("AudioKit namespaceName is invalid.");
        }
    }

    /// <summary>验证单个 C# 标识符。</summary>
    private static void ValidateIdentifier(string value, string name)
    {
        if (!IsIdentifier(value)) throw new InvalidDataException("AudioKit " + name + " is invalid.");
    }

    /// <summary>判断文本是否为基础 C# 标识符。</summary>
    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        for (var index = 1; index < value.Length; index++)
        {
            if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_')) return false;
        }

        return true;
    }
}
