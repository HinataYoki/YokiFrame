namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 UIKit Editor Tools 模板名与界面显示名之间的稳定映射。</summary>
public sealed partial class UIKitPageViewModel
{
    /// <summary>把 Provider 协议模板名转换为稳定排序的界面选项。</summary>
    private void ApplyCodeTemplateOptions(IReadOnlyList<string> templateNames)
    {
        List<string> names = new() { "Default", "Minimal" };
        if (templateNames != null)
        {
            for (var index = 0; index < templateNames.Count; index++)
            {
                string name = templateNames[index]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || ContainsTemplateName(names, name)) continue;
                names.Add(name);
            }
        }

        // Unity 编译或 Registry 刷新期间可能暂时漏报项目模板；已保存的选择仍必须留在 ComboBox 候选中。
        string configuredTemplateName = CodeTemplate?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(configuredTemplateName)
            && !ContainsTemplateName(names, configuredTemplateName))
        {
            names.Add(configuredTemplateName);
        }

        names.Sort(CompareCodeTemplateNames);
        string[] displayNames = new string[names.Count];
        for (var index = 0; index < names.Count; index++)
            displayNames[index] = GetCodeTemplateDisplayName(names[index]);
        mCodeTemplateNames = names;
        mCodeTemplateOptions = displayNames;
        OnPropertyChanged(nameof(CodeTemplateOptions));
        OnPropertyChanged(nameof(CodeTemplateDisplay));
    }

    /// <summary>保证当前模板选择存在于候选项中；候选仍不可用时回退到 Provider 默认项。</summary>
    /// <returns>发生回退时返回不可用的原模板名，否则返回空字符串。</returns>
    private string EnsureCodeTemplateSelection(string preferredTemplate)
    {
        if (ContainsTemplateName(mCodeTemplateNames, CodeTemplate)) return string.Empty;
        string unavailableTemplate = CodeTemplate;
        CodeTemplate = ContainsTemplateName(mCodeTemplateNames, preferredTemplate)
            ? preferredTemplate
            : "Default";
        return unavailableTemplate;
    }

    /// <summary>把内置模板本地化，自定义模板名保持原样以便识别。</summary>
    private static string GetCodeTemplateDisplayName(string templateName)
    {
        if (string.Equals(templateName, "Default", StringComparison.Ordinal)) return "默认";
        if (string.Equals(templateName, "Minimal", StringComparison.Ordinal)) return "精简";
        return templateName ?? string.Empty;
    }

    /// <summary>把界面显示名还原为提交给 Unity 的稳定模板名。</summary>
    private static string GetCodeTemplateName(string displayName)
    {
        if (string.Equals(displayName, "默认", StringComparison.Ordinal)) return "Default";
        if (string.Equals(displayName, "精简", StringComparison.Ordinal)) return "Minimal";
        return string.IsNullOrWhiteSpace(displayName) ? "Default" : displayName.Trim();
    }

    /// <summary>固定 Default、Minimal 在前，其余项目模板按 ordinal 排序。</summary>
    private static int CompareCodeTemplateNames(string left, string right)
    {
        int leftOrder = GetCodeTemplateOrder(left);
        int rightOrder = GetCodeTemplateOrder(right);
        return leftOrder != rightOrder
            ? leftOrder.CompareTo(rightOrder)
            : string.Compare(left, right, StringComparison.Ordinal);
    }

    /// <summary>返回模板排序分组，项目模板统一排在两个内置项之后。</summary>
    private static int GetCodeTemplateOrder(string templateName)
    {
        if (string.Equals(templateName, "Default", StringComparison.Ordinal)) return 0;
        if (string.Equals(templateName, "Minimal", StringComparison.Ordinal)) return 1;
        return 2;
    }

    /// <summary>按区分大小写的协议语义检查模板名是否已经存在。</summary>
    private static bool ContainsTemplateName(IReadOnlyList<string> names, string templateName)
    {
        for (var index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index], templateName, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
