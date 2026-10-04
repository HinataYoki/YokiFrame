using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 记录中的单个结构化字段。</summary>
public sealed class TableKitPreviewFieldViewModel
{
    /// <summary>从 Application 投影创建字段页面项。</summary>
    /// <param name="field">已完成类型和文本投影的字段。</param>
    public TableKitPreviewFieldViewModel(TableKitPreviewField field)
    {
        Name = field.Name;
        TypeName = field.TypeName;
        FullValueText = field.FullValueText;
        ValueText = field.ValueText;
        IsString = field.IsString;
        IsNumber = field.IsNumber;
        IsBoolean = field.IsBoolean;
        IsComplex = field.IsComplex;
    }

    /// <summary>字段名。</summary>
    public string Name { get; }

    /// <summary>字段类型名。</summary>
    public string TypeName { get; }

    /// <summary>受控长度的字段值预览。</summary>
    public string ValueText { get; }

    /// <summary>用于悬浮提示的受控长度字段值。</summary>
    public string FullValueText { get; }

    /// <summary>字段是否为字符串。</summary>
    public bool IsString { get; }

    /// <summary>字段是否为数字。</summary>
    public bool IsNumber { get; }

    /// <summary>字段是否为布尔值。</summary>
    public bool IsBoolean { get; }

    /// <summary>字段是否为数组或对象。</summary>
    public bool IsComplex { get; }
}
