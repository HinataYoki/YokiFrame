using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YokiFrame.Workbench.Avalonia.Converters;

/// <summary>把 ViewModel 输出的 #RRGGBB 颜色文本转换为界面 Brush，保持 ViewModel 不持有 UI 类型。</summary>
public sealed class HeatHexToBrushConverter : IValueConverter
{
    /// <summary>把 #RRGGBB 文本解析为 SolidColorBrush；非法文本回退为透明背景。</summary>
    /// <param name="value">ViewModel 提供的颜色文本。</param>
    /// <param name="targetType">Avalonia 目标属性类型。</param>
    /// <param name="parameter">未使用。</param>
    /// <param name="culture">当前界面区域信息；颜色文本格式固定不依赖它。</param>
    /// <returns>可绑定的 Brush。</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Brushes.Transparent;
        }

        try
        {
            var color = Color.Parse(text);
            return new SolidColorBrush(color);
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    /// <summary>颜色只用于显示，拒绝把界面值写回 ViewModel。</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
