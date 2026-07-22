namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>展示选中条目在单个语言下的普通文本和复数文本。</summary>
public sealed record LocalizationPreviewValueViewModel
{
    /// <summary>创建语言文本预览行。</summary>
    public LocalizationPreviewValueViewModel(string language, string value, string pluralValue, bool isMissing)
    {
        Language = language;
        Value = value;
        PluralValue = pluralValue;
        IsMissing = isMissing;
    }

    /// <summary>语言标识。</summary>
    public string Language { get; }
    /// <summary>普通文本；缺失时显示占位文本。</summary>
    public string Value { get; }
    /// <summary>复数分类和值的摘要。</summary>
    public string PluralValue { get; }
    /// <summary>是否缺失该语言。</summary>
    public bool IsMissing { get; }
    /// <summary>是否包含复数配置。</summary>
    public bool HasPlural => !string.IsNullOrWhiteSpace(PluralValue);
    /// <summary>是否已配置普通或复数文本。</summary>
    public bool IsComplete => !IsMissing;
    /// <summary>当前行状态。</summary>
    public string StateText => IsMissing ? "缺失" : HasPlural ? "复数" : "已配置";
}
