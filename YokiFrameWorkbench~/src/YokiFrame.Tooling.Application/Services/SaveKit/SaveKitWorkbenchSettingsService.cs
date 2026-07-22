using YokiFrame.Tooling.Application.Models.SaveKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.SaveKit;

/// <summary>通过统一项目配置 Store 读取、保存并扫描 SaveKit 项目配置。</summary>
public sealed class SaveKitWorkbenchSettingsService
{
    private const string UNITY_OWNER = "SaveKit";
    private const string GODOT_OWNER = "save_kit";
    private const string DEFAULT_EXTENSION = ".yoki";
    private const int MAX_EXTENSION_LENGTH = 32;
    private const string INVALID_EXTENSION_CHARACTERS = "<>:\"/\\|?*";
    private const string UNITY_DEFAULT_PATH = "${persistentDataPath}/YokiFrame/Saves";
    private const string GODOT_DEFAULT_PATH = "${userDataDir}/YokiFrame/Saves";
    private static readonly string[] sUnityOwnedKeys = { "storagePath", "fileExtension" };
    private static readonly string[] sGodotOwnedKeys = { "storage_path", "file_extension" };
    private readonly string mProjectRoot;
    private readonly YokiFrameProjectSettingsStore mSettingsStore;

    /// <summary>创建绑定一个规范化项目根的 SaveKit 配置服务。</summary>
    /// <param name="projectRoot">Unity 或 Godot 项目根。</param>
    public SaveKitWorkbenchSettingsService(string projectRoot)
        : this(new YokiFrameProjectSettingsStore(projectRoot))
    {
    }

    /// <summary>创建复用统一项目配置 Store 的 SaveKit 配置服务。</summary>
    /// <param name="settingsStore">项目级配置 Store。</param>
    public SaveKitWorkbenchSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        mProjectRoot = settingsStore.ProjectRoot;
    }

    /// <summary>读取当前引擎的 SaveKit 配置并扫描已存在的文件元信息。</summary>
    /// <param name="engineId">engine 标识，例如 unity-editor 或 godot-editor。</param>
    /// <returns>配置和扫描结果。</returns>
    public WorkbenchSaveKitProjectSettings Load(string engineId)
    {
        YokiFrameProjectSettingsTarget target = ResolveTarget(engineId);
        YokiFrameProjectSettingsSnapshot snapshot = mSettingsStore.Read(target);
        return CreateSettings(engineId, snapshot);
    }

    /// <summary>解析 Workbench 能确定的存档目录；运行时变量路径返回空字符串。</summary>
    /// <param name="storagePath">SaveKit 存档目录草稿。</param>
    /// <returns>可扫描的绝对路径，或空字符串表示需要 Runtime 解析。</returns>
    public string ResolveStoragePath(string storagePath)
    {
        storagePath ??= string.Empty;
        return ContainsRuntimeToken(storagePath) ? string.Empty : ResolveWorkbenchPath(storagePath);
    }

    /// <summary>校验 revision 后通过统一 Store 保存 SaveKit 配置。</summary>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="storagePath">存档目录，可使用宿主运行时变量。</param>
    /// <param name="fileExtension">存档扩展名。</param>
    /// <param name="expectedFingerprint">页面加载时看到的配置 revision。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存结果；外部修改时返回冲突而不覆盖。</returns>
    public async Task<SaveKitSettingsSaveResult> SaveAsync(
        string engineId,
        string storagePath,
        string fileExtension,
        string expectedFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFingerprint);
        storagePath = NormalizeStoragePath(storagePath, engineId);
        fileExtension = NormalizeExtension(fileExtension);
        YokiFrameProjectSettingsTarget target = ResolveTarget(engineId);
        YokiFrameProjectSettingsPatch patch = CreatePatch(target, storagePath, fileExtension);
        YokiFrameProjectSettingsWriteResult result = await mSettingsStore.WriteAsync(
            YokiFrameProjectSettingsUpdate.RequireRevision(expectedFingerprint, patch),
            cancellationToken).ConfigureAwait(false);
        WorkbenchSaveKitProjectSettings settings = CreateSettings(engineId, result.Snapshot);
        return new SaveKitSettingsSaveResult(
            result.Saved,
            result.ConflictDetected,
            settings,
            result.ConflictDetected ? "配置文件已被其它进程修改，请重新读取后再保存。" : string.Empty);
    }

    /// <summary>把统一 Store 快照转换为 SaveKit 项目 read model。</summary>
    private WorkbenchSaveKitProjectSettings CreateSettings(
        string engineId,
        YokiFrameProjectSettingsSnapshot snapshot)
    {
        bool isGodot = IsGodot(engineId);
        bool supported = isGodot || engineId.Contains("unity", StringComparison.OrdinalIgnoreCase);
        YokiFrameProjectSettingsTarget target = isGodot
            ? YokiFrameProjectSettingsTarget.GodotRuntime
            : YokiFrameProjectSettingsTarget.UnityRuntime;
        YokiFrameProjectSettingsDocument document = snapshot.GetDocument(target);
        IReadOnlyDictionary<string, string> values = document.GetValues(isGodot ? GODOT_OWNER : UNITY_OWNER);
        string storageKey = isGodot ? "storage_path" : "storagePath";
        string extensionKey = isGodot ? "file_extension" : "fileExtension";
        string storagePath = values.TryGetValue(storageKey, out string? configuredPath)
            ? configuredPath : (isGodot ? GODOT_DEFAULT_PATH : UNITY_DEFAULT_PATH);
        string extension = NormalizeExtension(
            values.TryGetValue(extensionKey, out string? configuredExtension) ? configuredExtension : DEFAULT_EXTENSION);
        return BuildProjectSettings(engineId, supported, document.Path, snapshot.Revision, storagePath, extension);
    }

    /// <summary>创建包含目录解析和文件元信息的最终项目设置。</summary>
    private WorkbenchSaveKitProjectSettings BuildProjectSettings(
        string engineId,
        bool supported,
        string configPath,
        string revision,
        string storagePath,
        string extension)
    {
        bool isGodot = IsGodot(engineId);
        string resolvedPath = ResolveWorkbenchPath(storagePath);
        bool containsRuntimeToken = ContainsRuntimeToken(storagePath);
        bool canScan = supported && !containsRuntimeToken;
        bool directoryExists = canScan && Directory.Exists(resolvedPath);
        IReadOnlyList<WorkbenchSaveKitFile> files = directoryExists
            ? ScanFiles(resolvedPath, extension)
            : Array.Empty<WorkbenchSaveKitFile>();
        string status = !supported ? "当前 engine 不支持 SaveKit 配置。"
            : containsRuntimeToken ? "目录包含运行时变量，实际路径将在游戏启动后解析。"
            : directoryExists ? "已读取存档目录元信息。" : "存档目录尚不存在，保存时会自动创建。";
        return new WorkbenchSaveKitProjectSettings(
            engineId, isGodot ? "Godot" : "Unity", supported, configPath, revision,
            storagePath, extension, resolvedPath, directoryExists, files, status);
    }

    /// <summary>创建只替换 SaveKit 自有键的统一 Store patch。</summary>
    private static YokiFrameProjectSettingsPatch CreatePatch(
        YokiFrameProjectSettingsTarget target,
        string storagePath,
        string extension)
    {
        if (target == YokiFrameProjectSettingsTarget.GodotRuntime)
        {
            return YokiFrameProjectSettingsPatch.ReplaceKeys(
                target,
                GODOT_OWNER,
                sGodotOwnedKeys,
                new YokiFrameProjectSettingValue("storage_path", storagePath),
                new YokiFrameProjectSettingValue("file_extension", extension));
        }

        return YokiFrameProjectSettingsPatch.ReplaceKeys(
            target,
            UNITY_OWNER,
            sUnityOwnedKeys,
            new YokiFrameProjectSettingValue("storagePath", storagePath),
            new YokiFrameProjectSettingValue("fileExtension", extension));
    }

    /// <summary>扫描 slots/global 下指定扩展名的文件，不读取 payload。</summary>
    private static IReadOnlyList<WorkbenchSaveKitFile> ScanFiles(string root, string extension)
    {
        List<WorkbenchSaveKitFile> files = new();
        ScanKind(files, root, "slots", "Slot", extension);
        ScanKind(files, root, "global", "Global", extension);
        return files.OrderByDescending(static file => file.LastWriteTimeUtc)
            .ThenBy(static file => file.FileName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>扫描一种 SaveKit 目标目录。</summary>
    private static void ScanKind(
        List<WorkbenchSaveKitFile> files,
        string root,
        string directoryName,
        string kind,
        string extension)
    {
        string directory = Path.Combine(root, directoryName);
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.EnumerateFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly))
        {
            FileInfo info = new(path);
            string fileName = info.Name[..^extension.Length];
            string name = kind == "Slot" && fileName.StartsWith("save_", StringComparison.Ordinal) ? fileName[5..] : fileName;
            files.Add(new WorkbenchSaveKitFile(kind, name, info.Name, info.FullName, info.Length, info.LastWriteTimeUtc));
        }
    }

    /// <summary>把 Workbench 可解析的路径转换为绝对路径。</summary>
    private string ResolveWorkbenchPath(string value)
    {
        if (ContainsRuntimeToken(value)) return string.Empty;
        return Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(Path.Combine(mProjectRoot, value));
    }

    /// <summary>规范化保存目录，保留运行时变量并拒绝空值。</summary>
    private static string NormalizeStoragePath(string value, string engineId)
    {
        if (string.IsNullOrWhiteSpace(value)) return IsGodot(engineId) ? GODOT_DEFAULT_PATH : UNITY_DEFAULT_PATH;
        return value.Trim().Replace('\\', '/');
    }

    /// <summary>规范化并验证扩展名，使 Workbench 与 Runtime 使用相同的安全文件名约束。</summary>
    private static string NormalizeExtension(string fileExtension)
    {
        fileExtension = string.IsNullOrWhiteSpace(fileExtension) ? DEFAULT_EXTENSION : fileExtension.Trim();
        if (!fileExtension.StartsWith(".", StringComparison.Ordinal))
        {
            fileExtension = "." + fileExtension;
        }

        if (fileExtension.Length < 2 || fileExtension.Length > MAX_EXTENSION_LENGTH ||
            fileExtension[^1] == '.')
        {
            throw new ArgumentException("文件扩展名必须包含 1 到 31 个有效字符。", nameof(fileExtension));
        }

        for (int index = 1; index < fileExtension.Length; index++)
        {
            char character = fileExtension[index];
            if (char.IsControl(character) || char.IsWhiteSpace(character) ||
                INVALID_EXTENSION_CHARACTERS.IndexOf(character) >= 0)
            {
                throw new ArgumentException("文件扩展名包含不支持的字符。", nameof(fileExtension));
            }
        }

        return fileExtension;
    }

    /// <summary>根据 engine 标识选择统一配置物理目标。</summary>
    private static YokiFrameProjectSettingsTarget ResolveTarget(string engineId)
    {
        return IsGodot(engineId)
            ? YokiFrameProjectSettingsTarget.GodotRuntime
            : YokiFrameProjectSettingsTarget.UnityRuntime;
    }

    /// <summary>判断 engine 标识是否属于 Godot。</summary>
    private static bool IsGodot(string engineId)
    {
        return engineId.Contains("godot", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>判断路径是否只能由宿主 Runtime 解析。</summary>
    private static bool ContainsRuntimeToken(string value)
    {
        return value.Contains("${persistentDataPath}", StringComparison.Ordinal)
               || value.Contains("${userDataDir}", StringComparison.Ordinal);
    }
}
