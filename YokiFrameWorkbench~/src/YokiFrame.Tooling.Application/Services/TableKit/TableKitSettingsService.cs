using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>通过统一项目配置 Store 分域保存 TableKit Workbench 草稿和 Runtime 设置。</summary>
public sealed class TableKitSettingsService
{
    private const string UNITY_RUNTIME_OWNER = "TableKit";
    private const string GODOT_RUNTIME_OWNER = "table_kit";
    // 替换范围保留已废弃键，使旧项目在下一次保存时自动清理历史配置。
    private static readonly string[] sUnityRuntimeKeys =
    {
        "runtimePathPattern", "useAsyncLoading", "useRawResourceLoading", "resourceRoot", "dataExtension"
    };
    private static readonly string[] sGodotRuntimeKeys =
    {
        "runtime_path_pattern", "use_async_loading", "use_raw_resource_loading", "resource_root", "data_extension"
    };
    private readonly YokiFrameProjectSettingsStore? mSettingsStore;
    private readonly TableKitResourceLocationResolver mResourceLocationResolver = new();

    /// <summary>创建按调用方项目根解析 Store 的 TableKit 配置服务。</summary>
    public TableKitSettingsService()
    {
    }

    /// <summary>创建复用指定项目配置 Store 的 TableKit 配置服务。</summary>
    /// <param name="settingsStore">项目级统一配置 Store。</param>
    public TableKitSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    /// <summary>读取项目级 TableKit 配置；文件不存在或 JSON 损坏时返回默认配置。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="defaults">页面默认配置。</param>
    /// <returns>绑定当前项目根的配置。</returns>
    public TableKitOptions Load(string projectRoot, TableKitOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        string normalizedRoot = Path.GetFullPath(projectRoot);
        TableKitOptions boundDefaults = RebindLoadedOptions(normalizedRoot, defaults);
        YokiFrameProjectOwnedDocumentSnapshot snapshot = GetStore(normalizedRoot)
            .ReadOwnedDocument(YokiFrameProjectSettingsTarget.TableKitDraft);
        if (!snapshot.Exists || string.IsNullOrWhiteSpace(snapshot.Content)) return boundDefaults;

        try
        {
            TableKitOptions? loaded = JsonSerializer.Deserialize(
                snapshot.Content,
                TableKitSettingsJsonContext.Default.TableKitOptions);
            return loaded == null ? boundDefaults : RebindLoadedOptions(normalizedRoot, loaded);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            return boundDefaults;
        }
    }

    /// <summary>分域提交 Workbench 草稿和两个 Runtime 设置，编辑器路径不会进入 Player 配置。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="options">待保存配置。</param>
    public void Save(string projectRoot, TableKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        string normalizedRoot = Path.GetFullPath(projectRoot);
        TableKitOptions normalizedOptions = RebindLoadedOptions(normalizedRoot, options);
        TableKitOptions portableOptions = CreatePortableOptions(normalizedRoot, normalizedOptions);
        YokiFrameProjectSettingsStore store = GetStore(normalizedRoot);
        string json = JsonSerializer.Serialize(
            portableOptions,
            TableKitSettingsJsonContext.Default.TableKitOptions);
        YokiFrameProjectOwnedDocumentWriteResult draftResult = store
            .WriteOwnedDocumentAsync(
                YokiFrameProjectSettingsTarget.TableKitDraft,
                json,
                YokiFrameProjectSettingsWriteMode.MergeLatest,
                cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!draftResult.Saved)
        {
            throw new IOException("TableKit Workbench settings changed while the draft was being committed.");
        }

        YokiFrameProjectSettingsPatch? runtimePatch = TryCreateRuntimePatch(normalizedRoot, normalizedOptions);
        if (runtimePatch == null || RuntimeSettingsMatch(store, runtimePatch)) return;
        YokiFrameProjectSettingsWriteResult runtimeResult = store.WriteAsync(
                YokiFrameProjectSettingsUpdate.MergeLatest(runtimePatch),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!runtimeResult.Saved)
        {
            throw new IOException("TableKit Runtime Settings changed while they were being committed.");
        }
    }

    /// <summary>把持久化草稿绑定到当前项目，并将旧项目根内的绝对路径重定位到当前根。</summary>
    /// <param name="projectRoot">当前规范化项目根。</param>
    /// <param name="options">默认配置、当前页面配置或旧草稿。</param>
    /// <returns>可直接用于当前项目验证和生成的配置。</returns>
    private static TableKitOptions RebindLoadedOptions(string projectRoot, TableKitOptions options)
    {
        string sourceRoot = ResolveSourceRoot(projectRoot, options.ProjectRoot);
        return options with
        {
            ProjectRoot = projectRoot,
            LubanConfigPath = ResolveOperationalPath(projectRoot, sourceRoot, options.LubanConfigPath),
            LubanWorkDir = ResolveOperationalPath(projectRoot, sourceRoot, options.LubanWorkDir),
            LubanExecutablePath = ResolveOperationalPath(projectRoot, sourceRoot, options.LubanExecutablePath),
            OutputDataDir = RebaseStoredPath(projectRoot, sourceRoot, options.OutputDataDir),
            OutputCodeDir = RebaseStoredPath(projectRoot, sourceRoot, options.OutputCodeDir),
            EditorDataPath = RebaseStoredPath(projectRoot, sourceRoot, options.EditorDataPath),
            ExtraOutputTargets = RebaseExtraOutputs(projectRoot, sourceRoot, options.ExtraOutputTargets)
        };
    }

    /// <summary>创建只含相对项目路径的可搬运草稿；项目外显式绝对工具路径保持原值。</summary>
    /// <param name="projectRoot">当前规范化项目根。</param>
    /// <param name="options">已经绑定当前项目的运行配置。</param>
    /// <returns>可安全写入项目设置目录的草稿。</returns>
    private static TableKitOptions CreatePortableOptions(string projectRoot, TableKitOptions options)
    {
        string sourceRoot = ResolveSourceRoot(projectRoot, options.ProjectRoot);
        return options with
        {
            ProjectRoot = ".",
            LubanConfigPath = RebaseStoredPath(projectRoot, sourceRoot, options.LubanConfigPath),
            LubanWorkDir = RebaseStoredPath(projectRoot, sourceRoot, options.LubanWorkDir),
            LubanExecutablePath = RebaseStoredPath(projectRoot, sourceRoot, options.LubanExecutablePath),
            OutputDataDir = RebaseStoredPath(projectRoot, sourceRoot, options.OutputDataDir),
            OutputCodeDir = RebaseStoredPath(projectRoot, sourceRoot, options.OutputCodeDir),
            EditorDataPath = RebaseStoredPath(projectRoot, sourceRoot, options.EditorDataPath),
            ExtraOutputTargets = RebaseExtraOutputs(projectRoot, sourceRoot, options.ExtraOutputTargets)
        };
    }

    /// <summary>重定位额外输出目录，并保持 target 与数据格式等非路径字段不变。</summary>
    /// <param name="projectRoot">当前规范化项目根。</param>
    /// <param name="sourceRoot">草稿原项目根。</param>
    /// <param name="outputs">待迁移的额外输出集合。</param>
    /// <returns>路径相对当前项目根的额外输出集合。</returns>
    private static IReadOnlyList<TableKitExtraOutput> RebaseExtraOutputs(
        string projectRoot,
        string sourceRoot,
        IReadOnlyList<TableKitExtraOutput>? outputs)
    {
        if (outputs == null || outputs.Count == 0) return Array.Empty<TableKitExtraOutput>();
        return outputs.Select(output => output with
        {
            OutputDataDir = RebaseStoredPath(projectRoot, sourceRoot, output.OutputDataDir),
            OutputCodeDir = RebaseStoredPath(projectRoot, sourceRoot, output.OutputCodeDir)
        }).ToArray();
    }

    /// <summary>把相对草稿路径解析为当前项目绝对路径，供 Luban 进程直接使用。</summary>
    /// <param name="projectRoot">当前规范化项目根。</param>
    /// <param name="sourceRoot">草稿原项目根。</param>
    /// <param name="path">草稿中的路径。</param>
    /// <returns>规范化绝对路径；空输入保持为空。</returns>
    private static string ResolveOperationalPath(string projectRoot, string sourceRoot, string path)
    {
        string rebased = RebaseStoredPath(projectRoot, sourceRoot, path);
        if (string.IsNullOrWhiteSpace(rebased)) return string.Empty;
        return Path.GetFullPath(Path.IsPathFullyQualified(rebased)
            ? rebased
            : Path.Combine(projectRoot, rebased));
    }

    /// <summary>把旧项目根内的绝对路径折叠为当前项目相对路径，并统一使用正斜杠。</summary>
    /// <param name="projectRoot">当前规范化项目根。</param>
    /// <param name="sourceRoot">草稿原项目根。</param>
    /// <param name="path">绝对路径或项目相对路径。</param>
    /// <returns>项目路径使用相对形式；无法相对化的外部绝对路径保持绝对形式。</returns>
    private static string RebaseStoredPath(string projectRoot, string sourceRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        bool preserveTrailingSeparator = path.EndsWith("/", StringComparison.Ordinal)
            || path.EndsWith("\\", StringComparison.Ordinal);
        string result;
        if (!Path.IsPathFullyQualified(path))
        {
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path));
            result = Path.GetRelativePath(projectRoot, fullPath);
        }
        else
        {
            string fullPath = Path.GetFullPath(path);
            if (!TryGetContainedRelativePath(sourceRoot, fullPath, out result)
                && !TryGetContainedRelativePath(projectRoot, fullPath, out result))
            {
                return NormalizePath(fullPath, preserveTrailingSeparator);
            }
        }

        return NormalizePath(result, preserveTrailingSeparator);
    }

    /// <summary>解析草稿声明的原项目根；相对根表示当前项目，兼容新的 `.` 存储格式。</summary>
    /// <param name="projectRoot">当前规范化项目根。</param>
    /// <param name="sourceRoot">草稿中的项目根。</param>
    /// <returns>用于重定位旧绝对路径的规范化根目录。</returns>
    private static string ResolveSourceRoot(string projectRoot, string sourceRoot)
    {
        return string.IsNullOrWhiteSpace(sourceRoot) || !Path.IsPathFullyQualified(sourceRoot)
            ? projectRoot
            : Path.GetFullPath(sourceRoot);
    }

    /// <summary>尝试取得根目录包含路径的相对形式，拒绝父目录逃逸与跨卷结果。</summary>
    /// <param name="root">候选包含根。</param>
    /// <param name="path">待判断绝对路径。</param>
    /// <param name="relativePath">成功时返回根目录相对路径。</param>
    /// <returns>路径位于根目录自身或其子目录时返回 true。</returns>
    private static bool TryGetContainedRelativePath(string root, string path, out string relativePath)
    {
        relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !Path.IsPathFullyQualified(relativePath)
            && relativePath != ".."
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>统一路径分隔符，并按原输入保留目录末尾斜杠。</summary>
    /// <param name="path">待规范化路径。</param>
    /// <param name="preserveTrailingSeparator">是否保留目录末尾斜杠。</param>
    /// <returns>使用正斜杠的稳定路径。</returns>
    private static string NormalizePath(string path, bool preserveTrailingSeparator)
    {
        string normalized = path.Replace('\\', '/');
        return preserveTrailingSeparator && normalized != "." && !normalized.EndsWith("/", StringComparison.Ordinal)
            ? normalized + "/"
            : normalized;
    }

    /// <summary>仅在草稿已能解析出有效宿主资源定位时创建 Runtime Settings patch。</summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="options">绑定当前项目根的完整草稿。</param>
    /// <returns>可提交 patch；草稿尚不完整或宿主未知时返回 null。</returns>
    private YokiFrameProjectSettingsPatch? TryCreateRuntimePatch(
        string projectRoot,
        TableKitOptions options)
    {
        try
        {
            TableKitRuntimeLocation runtimeLocation = mResourceLocationResolver.Resolve(options);
            return CreateRuntimePatch(
                projectRoot,
                runtimeLocation.PathPattern,
                options.UseRawResourceLoading);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>根据唯一宿主创建只替换 TableKit 自有运行时键的配置 patch。</summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="runtimePathPattern">已解析的最终资源路径模板。</param>
    /// <param name="useRawResourceLoading">Loader 是否使用原始资源能力。</param>
    /// <returns>Unity JSON 或 Godot ProjectSettings patch；宿主未知或冲突时返回 null。</returns>
    private static YokiFrameProjectSettingsPatch? CreateRuntimePatch(
        string projectRoot,
        string runtimePathPattern,
        bool useRawResourceLoading)
    {
        bool isUnity = File.Exists(Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt"));
        bool isGodot = File.Exists(Path.Combine(projectRoot, "project.godot"));
        if (isUnity == isGodot)
        {
            return null;
        }

        return isUnity
            ? YokiFrameProjectSettingsPatch.ReplaceKeys(
                YokiFrameProjectSettingsTarget.UnityRuntime,
                UNITY_RUNTIME_OWNER,
                sUnityRuntimeKeys,
                new YokiFrameProjectSettingValue("runtimePathPattern", runtimePathPattern),
                new YokiFrameProjectSettingValue("useRawResourceLoading", ToSettingValue(useRawResourceLoading)))
            : YokiFrameProjectSettingsPatch.ReplaceKeys(
                YokiFrameProjectSettingsTarget.GodotRuntime,
                GODOT_RUNTIME_OWNER,
                sGodotRuntimeKeys,
                new YokiFrameProjectSettingValue("runtime_path_pattern", runtimePathPattern),
                new YokiFrameProjectSettingValue("use_raw_resource_loading", ToSettingValue(useRawResourceLoading)));
    }

    /// <summary>比较当前文档中 TableKit 自有键，避免相同 Runtime Settings 被重复替换。</summary>
    /// <param name="store">绑定当前项目的统一配置 Store。</param>
    /// <param name="patch">待提交的 TableKit Runtime patch。</param>
    /// <returns>所有替换键已与目标值一致时返回 true。</returns>
    private static bool RuntimeSettingsMatch(
        YokiFrameProjectSettingsStore store,
        YokiFrameProjectSettingsPatch patch)
    {
        IReadOnlyDictionary<string, string> current = store.Read(patch.Target)
            .GetDocument(patch.Target)
            .GetValues(patch.Owner);
        Dictionary<string, string> expected = patch.Values.ToDictionary(
            static value => value.Key,
            static value => value.Value,
            StringComparer.Ordinal);
        foreach (string key in patch.ReplacedKeys)
        {
            bool hasCurrent = current.TryGetValue(key, out string? currentValue);
            bool hasExpected = expected.TryGetValue(key, out string? expectedValue);
            if (hasCurrent != hasExpected || hasCurrent && !string.Equals(currentValue, expectedValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>把布尔值转换为 Runtime Settings 统一使用的小写标量。</summary>
    /// <param name="value">待保存布尔值。</param>
    /// <returns>小写 true 或 false。</returns>
    private static string ToSettingValue(bool value)
    {
        return value ? "true" : "false";
    }

    /// <summary>获取绑定当前项目根的统一配置 Store，并拒绝跨项目复用。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <returns>项目配置 Store。</returns>
    private YokiFrameProjectSettingsStore GetStore(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        string normalizedRoot = Path.GetFullPath(projectRoot);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (mSettingsStore != null)
        {
            if (!string.Equals(mSettingsStore.ProjectRoot, normalizedRoot, comparison))
            {
                throw new InvalidOperationException("TableKit settings store is bound to a different project root.");
            }

            return mSettingsStore;
        }

        return new YokiFrameProjectSettingsStore(normalizedRoot);
    }
}

/// <summary>为 TableKit 项目配置提供 Native AOT 可用的 JSON 元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TableKitOptions))]
internal sealed partial class TableKitSettingsJsonContext : JsonSerializerContext
{
}
