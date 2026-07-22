namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Workbench 页面中的一段标题和值文本。
/// </summary>
public sealed class WorkbenchDisplaySection
{
    /// <summary>
    /// 创建页面显示段落。
    /// </summary>
    /// <param name="label">段落标题。</param>
    /// <param name="value">段落正文。</param>
    public WorkbenchDisplaySection(string label, string value)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? "none" : value;
    }

    /// <summary>
    /// 获取段落标题。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 获取段落正文。
    /// </summary>
    public string Value { get; }
}
