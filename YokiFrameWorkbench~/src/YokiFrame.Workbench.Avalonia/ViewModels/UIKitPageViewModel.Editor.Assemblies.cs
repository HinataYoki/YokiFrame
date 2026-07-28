namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 UIKit Editor Tools 目标程序集的稳定选项和回退规则。</summary>
public sealed partial class UIKitPageViewModel
{
    private const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";

    private IReadOnlyList<string> mAssemblyNames = new[] { DEFAULT_ASSEMBLY_NAME };

    /// <summary>获取 Unity Editor 当前可供生成代码选择的程序集名称。</summary>
    public IReadOnlyList<string> AssemblyNames => mAssemblyNames;

    /// <summary>将配置中的程序集补入 ComboBox 候选，确保 TwoWay 绑定不会把恢复值清空。</summary>
    /// <param name="assemblyName">待加入的程序集名称。</param>
    private void EnsureAssemblyOption(string assemblyName)
    {
        string normalizedAssemblyName = assemblyName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedAssemblyName)
            || ContainsAssemblyName(mAssemblyNames, normalizedAssemblyName)) return;

        List<string> names = new(mAssemblyNames) { normalizedAssemblyName };
        names.Sort(CompareAssemblyNames);
        mAssemblyNames = names.ToArray();
        OnPropertyChanged(nameof(AssemblyNames));
    }

    /// <summary>
    /// 应用 Unity Editor 扫描到的程序集，并保留当前配置值，避免 context 暂时漏报时覆盖已保存选择。
    /// </summary>
    /// <param name="assemblyNames">Unity Editor 返回的可用程序集列表。</param>
    private void ApplyAssemblyOptions(IReadOnlyList<string> assemblyNames)
    {
        List<string> names = new() { DEFAULT_ASSEMBLY_NAME };
        if (assemblyNames != null)
        {
            for (var index = 0; index < assemblyNames.Count; index++)
            {
                string assemblyName = assemblyNames[index]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(assemblyName)
                    || ContainsAssemblyName(names, assemblyName)) continue;
                names.Add(assemblyName);
            }
        }

        // Unity 在重新编译或刚启动时可能暂时只返回默认程序集；当前配置仍是有效的用户意图，必须留在候选项中。
        string configuredAssemblyName = AssemblyName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(configuredAssemblyName)
            && !ContainsAssemblyName(names, configuredAssemblyName))
        {
            names.Add(configuredAssemblyName);
        }

        names.Sort(CompareAssemblyNames);
        mAssemblyNames = names.ToArray();
        OnPropertyChanged(nameof(AssemblyNames));
        if (string.IsNullOrWhiteSpace(AssemblyName))
            AssemblyName = DEFAULT_ASSEMBLY_NAME;
    }

    /// <summary>确保默认程序集始终排在首位，其余程序集按 ordinal 顺序稳定排列。</summary>
    /// <param name="left">待比较的左侧程序集名。</param>
    /// <param name="right">待比较的右侧程序集名。</param>
    /// <returns>排序比较结果。</returns>
    private static int CompareAssemblyNames(string left, string right)
    {
        bool leftIsDefault = string.Equals(left, DEFAULT_ASSEMBLY_NAME, StringComparison.Ordinal);
        bool rightIsDefault = string.Equals(right, DEFAULT_ASSEMBLY_NAME, StringComparison.Ordinal);
        if (leftIsDefault != rightIsDefault) return leftIsDefault ? -1 : 1;
        return string.Compare(left, right, StringComparison.Ordinal);
    }

    /// <summary>按程序集的区分大小写语义判断候选列表是否已有指定名称。</summary>
    /// <param name="assemblyNames">待检查的程序集候选列表。</param>
    /// <param name="assemblyName">待匹配的程序集名称。</param>
    /// <returns>找到相同程序集名称时返回 true。</returns>
    private static bool ContainsAssemblyName(IReadOnlyList<string> assemblyNames, string assemblyName)
    {
        for (var index = 0; index < assemblyNames.Count; index++)
        {
            if (string.Equals(assemblyNames[index], assemblyName, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
