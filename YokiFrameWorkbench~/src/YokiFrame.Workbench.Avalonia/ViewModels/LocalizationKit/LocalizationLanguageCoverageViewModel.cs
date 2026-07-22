namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>展示单个语言在当前目录中的覆盖情况。</summary>
public sealed record LocalizationLanguageCoverageViewModel
{
    /// <summary>创建语言覆盖摘要。</summary>
    public LocalizationLanguageCoverageViewModel(string language, int presentCount, int missingCount)
    {
        Language = language;
        PresentCount = presentCount;
        MissingCount = missingCount;
    }

    /// <summary>语言标识。</summary>
    public string Language { get; }
    /// <summary>已配置条目数量。</summary>
    public int PresentCount { get; }
    /// <summary>缺失条目数量。</summary>
    public int MissingCount { get; }
    /// <summary>覆盖数量文本。</summary>
    public string CoverageText => PresentCount + " / " + (PresentCount + MissingCount);
    /// <summary>覆盖率百分比，用于目录级进度条。</summary>
    public double CoveragePercent
    {
        get
        {
            int totalCount = PresentCount + MissingCount;
            return totalCount == 0 ? 0d : PresentCount * 100d / totalCount;
        }
    }
    /// <summary>覆盖状态文本。</summary>
    public string StateText => MissingCount == 0 ? "完整" : "缺 " + MissingCount;
    /// <summary>是否存在缺失条目。</summary>
    public bool HasMissing => MissingCount > 0;
    /// <summary>是否所有条目均已配置。</summary>
    public bool IsComplete => !HasMissing;
}
