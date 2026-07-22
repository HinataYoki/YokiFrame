using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

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
        return value switch
        {
            "Preloaded" => "已预加载",
            "Opening" => "打开中",
            "Open" => "已打开",
            "Hiding" => "隐藏中",
            "Hide" or "Hidden" => "已隐藏",
            "Closing" => "关闭中",
            "Cached" => "已缓存",
            "Close" or "Closed" => "已关闭",
            _ => value,
        };
    }

    /// <summary>转换 UIKit 预定义 UILevel 名称，自定义层级保持原始值。</summary>
    private static string ConvertLevel(string value)
    {
        return value switch
        {
            "AlwayBottom" => "最底层",
            "Bg" => "背景层",
            "Hud" => "HUD 层",
            "Common" => "常规层",
            "Toast" => "轻提示层",
            "Pop" or "PopUI" => "弹窗层",
            "Guide" => "引导层",
            "AlwayTop" => "最顶层",
            "CanvasPanel" => "独立画布层",
            _ => value,
        };
    }

    /// <summary>转换 UIKit PanelCachePolicy 名称。</summary>
    private static string ConvertCachePolicy(string value)
    {
        return value switch
        {
            "Transient" => "临时",
            "Reusable" => "可复用",
            "Persistent" => "持久",
            _ => value,
        };
    }

    /// <summary>把布尔诊断值转换为“是”或“否”。</summary>
    private static string ConvertBoolean(object? value)
    {
        return value is bool boolean ? boolean ? "是" : "否" : value?.ToString() ?? string.Empty;
    }
}
