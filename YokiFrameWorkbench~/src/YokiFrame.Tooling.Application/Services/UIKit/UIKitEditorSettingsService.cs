using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Services.Settings;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Tooling.Application.Services.UIKit;

/// <summary>通过统一项目配置 Store 读取和保存 UIKit Editor Tools 生成配置。</summary>
public sealed class UIKitEditorSettingsService
{
    private const string UI_KIT = "UIKit";
    private const string PREFAB_FOLDER_KEY = "editor.prefabFolder";
    private const string SCRIPT_FOLDER_KEY = "editor.scriptFolder";
    private const string SCRIPT_NAMESPACE_KEY = "editor.scriptNamespace";
    private const string ASSEMBLY_NAME_KEY = "editor.assemblyName";
    private const string CODE_TEMPLATE_KEY = "editor.codeTemplate";
    private static readonly string[] sOwnedKeys =
    {
        PREFAB_FOLDER_KEY,
        SCRIPT_FOLDER_KEY,
        SCRIPT_NAMESPACE_KEY,
        ASSEMBLY_NAME_KEY,
        CODE_TEMPLATE_KEY,
    };
    private readonly string mProjectRoot;
    private readonly YokiFrameProjectSettingsStore mSettingsStore;

    /// <summary>创建绑定当前 Unity 项目根的 UIKit Editor Tools 设置服务。</summary>
    /// <param name="projectRoot">当前 Unity 项目根。</param>
    public UIKitEditorSettingsService(string projectRoot)
        : this(new YokiFrameProjectSettingsStore(projectRoot))
    {
    }

    /// <summary>创建复用项目级统一配置 Store 的 UIKit Editor Tools 设置服务。</summary>
    /// <param name="settingsStore">项目级配置 Store。</param>
    public UIKitEditorSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        mProjectRoot = settingsStore.ProjectRoot;
    }

    /// <summary>获取受统一 Store 管理的 Unity Editor Project Settings 路径。</summary>
    public string SettingsPath => mSettingsStore.GetPath(YokiFrameProjectSettingsTarget.UnityEditor);

    /// <summary>读取已保存的 Editor Tools 配置；未配置时返回空，由 Unity Provider 提供默认值。</summary>
    /// <returns>经过校验的生成配置，或空表示项目尚未保存覆盖值。</returns>
    public WorkbenchUIKitPanelGenerationRequest? Load()
    {
        YokiFrameProjectSettingsSnapshot snapshot = mSettingsStore.Read(
            YokiFrameProjectSettingsTarget.UnityEditor);
        IReadOnlyDictionary<string, string> values = snapshot
            .GetDocument(YokiFrameProjectSettingsTarget.UnityEditor)
            .GetValues(UI_KIT);
        if (!sOwnedKeys.Any(values.ContainsKey)) return null;

        WorkbenchUIKitPanelGenerationRequest defaults = new();
        return ValidateAndNormalize(new WorkbenchUIKitPanelGenerationRequest
        {
            PrefabFolder = ReadString(values, PREFAB_FOLDER_KEY, defaults.PrefabFolder),
            ScriptFolder = ReadString(values, SCRIPT_FOLDER_KEY, defaults.ScriptFolder),
            ScriptNamespace = ReadString(values, SCRIPT_NAMESPACE_KEY, defaults.ScriptNamespace),
            AssemblyName = ReadString(values, ASSEMBLY_NAME_KEY, defaults.AssemblyName),
            CodeTemplate = ReadString(values, CODE_TEMPLATE_KEY, defaults.CodeTemplate),
        });
    }

    /// <summary>校验并保存 Editor Tools 的五个项目级生成字段。</summary>
    /// <param name="settings">当前页面生成配置；PanelName 不属于持久配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>配置提交完成任务。</returns>
    public async Task SaveAsync(
        WorkbenchUIKitPanelGenerationRequest settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        WorkbenchUIKitPanelGenerationRequest normalized = ValidateAndNormalize(settings);
        YokiFrameProjectSettingsWriteResult result = await mSettingsStore.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(CreatePatch(normalized)),
            cancellationToken).ConfigureAwait(false);
        if (!result.Saved) throw new IOException("UIKit Editor Tools 配置提交失败。");
    }

    /// <summary>创建只替换 UIKit Editor Tools 五个声明键的统一 Store patch。</summary>
    private static YokiFrameProjectSettingsPatch CreatePatch(
        WorkbenchUIKitPanelGenerationRequest settings)
    {
        return YokiFrameProjectSettingsPatch.ReplaceKeys(
            YokiFrameProjectSettingsTarget.UnityEditor,
            UI_KIT,
            sOwnedKeys,
            new YokiFrameProjectSettingValue(PREFAB_FOLDER_KEY, settings.PrefabFolder),
            new YokiFrameProjectSettingValue(SCRIPT_FOLDER_KEY, settings.ScriptFolder),
            new YokiFrameProjectSettingValue(SCRIPT_NAMESPACE_KEY, settings.ScriptNamespace),
            new YokiFrameProjectSettingValue(ASSEMBLY_NAME_KEY, settings.AssemblyName),
            new YokiFrameProjectSettingValue(CODE_TEMPLATE_KEY, settings.CodeTemplate));
    }

    /// <summary>校验路径、代码命名和模板安全 ID，并返回可稳定持久化的副本。</summary>
    private WorkbenchUIKitPanelGenerationRequest ValidateAndNormalize(
        WorkbenchUIKitPanelGenerationRequest settings)
    {
        string prefabFolder = NormalizeAssetsFolder(settings.PrefabFolder, "预制体目录");
        string scriptFolder = NormalizeAssetsFolder(settings.ScriptFolder, "脚本目录");
        string scriptNamespace = NormalizeNamespace(settings.ScriptNamespace);
        string assemblyName = NormalizeAssemblyName(settings.AssemblyName);
        string codeTemplate = NormalizeCodeTemplate(settings.CodeTemplate);
        return new WorkbenchUIKitPanelGenerationRequest
        {
            PrefabFolder = prefabFolder,
            ScriptFolder = scriptFolder,
            ScriptNamespace = scriptNamespace,
            AssemblyName = assemblyName,
            CodeTemplate = codeTemplate,
        };
    }

    /// <summary>规范化 Unity Assets 相对目录并拒绝绝对路径和项目逃逸。</summary>
    private string NormalizeAssetsFolder(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(fieldName + "不能为空。", nameof(value));
        string normalized = value.Trim().Replace('\\', '/').TrimEnd('/');
        if (Path.IsPathRooted(normalized)
            || !(string.Equals(normalized, "Assets", StringComparison.Ordinal)
                || normalized.StartsWith("Assets/", StringComparison.Ordinal))
            || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(static segment => segment is "." or ".."))
            throw new ArgumentException(fieldName + "必须是项目内的 Assets 相对目录。", nameof(value));

        string candidate = Path.GetFullPath(Path.Combine(mProjectRoot, normalized));
        string relative = Path.GetRelativePath(mProjectRoot, candidate);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException(fieldName + "不能超出当前项目。", nameof(value));
        return normalized;
    }

    /// <summary>规范化点分隔 C# 命名空间并拒绝非法标识符。</summary>
    private static string NormalizeNamespace(string value)
    {
        string normalized = RequireText(value, "命名空间");
        string[] segments = normalized.Split('.');
        if (segments.Length == 0 || segments.Any(static segment => !IsIdentifier(segment)))
            throw new ArgumentException("命名空间必须由有效的 C# 标识符组成。", nameof(value));
        return normalized;
    }

    /// <summary>规范化程序集名称并拒绝路径字符和控制字符。</summary>
    private static string NormalizeAssemblyName(string value)
    {
        string normalized = RequireText(value, "目标程序集");
        if (normalized.Length > 256 || normalized.IndexOfAny(new[] { '/', '\\' }) >= 0
            || normalized.Any(char.IsControl))
            throw new ArgumentException("目标程序集名称无效。", nameof(value));
        return normalized;
    }

    /// <summary>
    /// 把内置模板规范为稳定协议值，并允许项目注册的安全 ID 模板名原样保存。
    /// Application 层不探测 Unity TypeCache，模板是否当前可用由 Editor Provider 在执行时校验。
    /// </summary>
    private static string NormalizeCodeTemplate(string value)
    {
        string normalized = RequireText(value, "代码模板");
        if (string.Equals(normalized, "Default", StringComparison.OrdinalIgnoreCase)) return "Default";
        if (string.Equals(normalized, "Minimal", StringComparison.OrdinalIgnoreCase)) return "Minimal";
        if (SafeIdValidator.IsSafeId(normalized)) return normalized;
        throw new ArgumentException(
            "代码模板必须是 Default、Minimal 或符合安全 ID 规则的项目模板名。",
            nameof(value));
    }

    /// <summary>读取稀疏配置值，缺失时返回代码默认值。</summary>
    private static string ReadString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback)
    {
        return values.TryGetValue(key, out string? value) ? value : fallback;
    }

    /// <summary>要求配置文本非空并返回裁剪值。</summary>
    private static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(fieldName + "不能为空。", nameof(value));
        return value.Trim();
    }

    /// <summary>判断文本是否为不含关键字转义的基础 C# 标识符。</summary>
    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        for (int index = 1; index < value.Length; index++)
        {
            if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_')) return false;
        }

        return true;
    }
}
