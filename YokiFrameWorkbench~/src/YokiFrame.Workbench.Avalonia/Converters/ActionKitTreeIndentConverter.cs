using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace YokiFrame.Workbench.Avalonia.Converters;

/// <summary>把 TreeView 层级转换为 ActionKit 封顶缩进，避免深层动作树耗尽正文宽度。</summary>
public sealed class ActionKitTreeIndentConverter : IValueConverter
{
    private const int MAX_INDENT_LEVEL = 6;
    private const double INDENT_WIDTH = 10D;

    /// <summary>创建 ActionKit 动作树缩进转换器。</summary>
    public ActionKitTreeIndentConverter()
    {
    }

    /// <summary>将树层级限制在六级可见缩进内，深度信息由节点徽章继续表达。</summary>
    /// <param name="value">Avalonia TreeViewItem 的绝对层级。</param>
    /// <param name="targetType">目标属性类型。</param>
    /// <param name="parameter">未使用的转换参数。</param>
    /// <param name="culture">当前文化信息。</param>
    /// <returns>只包含左侧缩进的 Thickness。</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int level = value is int itemLevel ? itemLevel : 0;
        double indent = Math.Min(Math.Max(level, 0), MAX_INDENT_LEVEL) * INDENT_WIDTH;
        return new Thickness(indent, 0D, 0D, 0D);
    }

    /// <summary>树缩进是只读派生值，不支持从布局值回写层级。</summary>
    /// <param name="value">目标端 Thickness。</param>
    /// <param name="targetType">源属性类型。</param>
    /// <param name="parameter">未使用的转换参数。</param>
    /// <param name="culture">当前文化信息。</param>
    /// <returns>始终返回不执行绑定回写。</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
