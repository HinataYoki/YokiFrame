using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Converters;

/// <summary>把 UIKit 运行时协议枚举转换为简体中文界面文本，不修改强类型诊断模型。</summary>
public sealed class UIKitDisplayTextConverter : IValueConverter
{
    /// <summary>按 converter parameter 选择生命周期、层级、缓存或布尔值映射。</summary>
    /// <param name="value">协议模型中的原始值。</param>
    /// <param name="targetType">Avalonia 目标属性类型。</param>
    /// <param name="parameter">映射类别：state、level、cache 或 boolean。</param>
    /// <param name="culture">当前界面区域信息；固定映射不依赖它。</param>
    /// <returns>已知协议值的中文名称，未知值保持原样。</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value?.ToString() ?? string.Empty;
        return parameter switch
        {
            "state" => ConvertState(text),
            "level" => ConvertLevel(text),
            "cache" => ConvertCachePolicy(text),
            "boolean" => ConvertBoolean(value),
            _ => text,
        };
    }

    /// <summary>诊断文本只支持单向显示，拒绝把中文名称写回协议模型。</summary>
    /// <param name="value">界面尝试回写的值。</param>
    /// <param name="targetType">源属性类型。</param>
    /// <param name="parameter">原转换参数。</param>
    /// <param name="culture">当前界面区域信息。</param>
    /// <returns>始终返回 DoNothing，保持原始诊断数据只读。</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }

    /// <summary>转换 UIKit PanelState 名称。</summary>
    private static string ConvertState(string value)
    {
        string normalized = value switch
        {
            "Hide" => "Hidden",
            "Close" => "Closed",
            _ => value,
        };
        return WorkbenchI18nService.Instance.GetString("String.UIKit.State." + normalized, value);
    }

    /// <summary>转换 UIKit 预定义 UILevel 名称，自定义层级保持原始值。</summary>
    private static string ConvertLevel(string value)
    {
        return WorkbenchI18nService.Instance.GetString("String.UIKit.Level." + value, value);
    }

    /// <summary>转换 UIKit PanelCachePolicy 名称。</summary>
    private static string ConvertCachePolicy(string value)
    {
        return WorkbenchI18nService.Instance.GetString("String.UIKit.Cache." + value, value);
    }

    /// <summary>把布尔诊断值转换为“是”或“否”。</summary>
    private static string ConvertBoolean(object? value)
    {
        if (value is bool boolean)
        {
            return boolean
                ? WorkbenchI18nService.Instance.GetString("String.Common.Yes", "是")
                : WorkbenchI18nService.Instance.GetString("String.Common.No", "否");
        }
        return value?.ToString() ?? string.Empty;
    }
}
