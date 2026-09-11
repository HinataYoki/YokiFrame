using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// Workbench 跨界面多语言（i18n / 本地化）服务。
/// 字符串表是唯一事实源，按语言拆分在 WorkbenchI18nService.Strings.*.cs；
/// 切换语言时把当前语言的键值写入 Application 资源，驱动 DynamicResource 全树刷新。
/// </summary>
public sealed partial class WorkbenchI18nService
{
    private const string ZH_CN = "zh-CN";
    private const string EN_US = "en-US";
    private const string ZH_DISPLAY = "中文";
    private const string EN_DISPLAY = "English";

    private static readonly Lazy<WorkbenchI18nService> sInstance = new(() => new WorkbenchI18nService());

    /// <summary>获取多语言服务的全局共享单例。</summary>
    public static WorkbenchI18nService Instance => sInstance.Value;

    private static readonly IReadOnlyList<string> sCultureOptions = new[] { ZH_DISPLAY, EN_DISPLAY };

    private string mCurrentCultureName = ZH_CN;

    /// <summary>语言切换事件，通知 ViewModel 重新投影动态文本。</summary>
    public event Action? CultureChanged;

    /// <summary>获取所有可用的语言显示名称列表。</summary>
    public IReadOnlyList<string> CultureOptions => sCultureOptions;

    /// <summary>获取当前生效的语言标准名称（如 "zh-CN", "en-US"）。</summary>
    public string CurrentCultureName => mCurrentCultureName;

    /// <summary>获取当前生效的语言显示名称（如 "中文", "English"）。</summary>
    public string CurrentCultureDisplayName => mCurrentCultureName == EN_US ? EN_DISPLAY : ZH_DISPLAY;

    private WorkbenchI18nService()
    {
        mCurrentCultureName = LoadPersistedCulture();
    }

    /// <summary>语言偏好持久化文件路径（%APPDATA%/YokiFrame/workbench-culture.txt）。</summary>
    private static string CultureFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YokiFrame",
        "workbench-culture.txt");

    /// <summary>从用户 AppData 目录读取上次保存的语言偏好；文件缺失或损坏时回落中文。</summary>
    private static string LoadPersistedCulture()
    {
        try
        {
            var path = CultureFilePath;
            if (!File.Exists(path)) return ZH_CN;
            var saved = File.ReadAllText(path).Trim();
            return saved is EN_US or ZH_CN ? saved : ZH_CN;
        }
        catch (Exception)
        {
            return ZH_CN;
        }
    }

    /// <summary>将当前语言偏好写入用户 AppData 目录。</summary>
    private static void PersistCulture(string cultureName)
    {
        try
        {
            var path = CultureFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, cultureName);
        }
        catch (Exception)
        {
            // 持久化失败不阻断语言切换；下次启动回落中文。
        }
    }

    /// <summary>
    /// 根据显示名称切换语言（"中文" 或 "English"）。
    /// </summary>
    /// <param name="displayName">语言显示名称。</param>
    /// <returns>切换成功返回 true；未发生变化或未知选项返回 false。</returns>
    public bool SetCultureByDisplayName(string displayName)
    {
        var targetCulture = string.Equals(displayName, EN_DISPLAY, StringComparison.OrdinalIgnoreCase)
            ? EN_US
            : string.Equals(displayName, ZH_DISPLAY, StringComparison.OrdinalIgnoreCase)
                ? ZH_CN
                : null;
        if (targetCulture == null)
        {
            return false;
        }

        return SetCulture(targetCulture);
    }

    /// <summary>
    /// 根据标准文化名称切换语言（"zh-CN" 或 "en-US"）。
    /// </summary>
    /// <param name="cultureName">标准文化名称。</param>
    /// <returns>切换成功返回 true。</returns>
    public bool SetCulture(string cultureName)
    {
        var normalized = string.Equals(cultureName, EN_US, StringComparison.OrdinalIgnoreCase)
            ? EN_US
            : string.Equals(cultureName, ZH_CN, StringComparison.OrdinalIgnoreCase)
                ? ZH_CN
                : null;
        if (normalized == null)
        {
            return false;
        }

        if (string.Equals(mCurrentCultureName, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        mCurrentCultureName = normalized;
        PersistCulture(normalized);

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyToApplicationResources(normalized);
            CultureChanged?.Invoke();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                ApplyToApplicationResources(normalized);
                CultureChanged?.Invoke();
            });
        }
        return true;
    }

    /// <summary>
    /// 从当前语言资源中查找指定 key 的本地化字符串。
    /// </summary>
    /// <param name="key">多语言 key（如 "String.Overview.Title"）。</param>
    /// <param name="fallback">未找到时的兜底文本；传 null 时返回 key 自身。</param>
    /// <returns>本地化文本。</returns>
    public string GetString(string key, string? fallback = null)
    {
        var stringTable = mCurrentCultureName == EN_US ? sEnStrings : sZhStrings;
        if (stringTable.TryGetValue(key, out var staticText))
        {
            return staticText;
        }

        if (Application.Current?.Resources != null)
        {
            if (Application.Current.Resources.TryGetResource(key, null, out var value) && value is string text)
            {
                return text;
            }
        }

        return fallback ?? key;
    }

    /// <summary>
    /// 把当前语言的键值写入 Application 资源；由启动流程和语言切换共同调用。
    /// 必须在 AvaloniaXamlLoader.Load 之后调用，否则 DynamicResource 在首帧找不到键。
    /// </summary>
    public void ApplyCurrentCulture()
    {
        ApplyToApplicationResources(mCurrentCultureName);
    }

    /// <summary>
    /// 动态将指定语言的全部键值写入 Application.Current.Resources，即时触发 Avalonia 全 Visual 树 DynamicResource 刷新。
    /// </summary>
    private static void ApplyToApplicationResources(string cultureName)
    {
        if (Application.Current?.Resources is not ResourceDictionary appResources)
        {
            return;
        }

        var table = cultureName == EN_US ? sEnStrings : sZhStrings;
        foreach (var (key, val) in table)
        {
            appResources[key] = val;
        }
    }
}
