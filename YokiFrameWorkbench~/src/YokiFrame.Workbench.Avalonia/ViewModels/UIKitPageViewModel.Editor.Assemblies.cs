namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 UIKit Editor Tools 目标程序集的稳定选项和回退规则。</summary>
public sealed partial class UIKitPageViewModel
{
    private const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";

    private IReadOnlyList<string> mAssemblyNames = new[] { DEFAULT_ASSEMBLY_NAME };

    /// <summary>获取 Unity Editor 当前可供生成代码选择的程序集名称。</summary>
    public IReadOnlyList<string> AssemblyNames => mAssemblyNames;

    /// <summary>应用 Unity Editor 扫描到的程序集，并让失效的已保存值回退到默认程序集。</summary>
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

        names.Sort(CompareAssemblyNames);
        mAssemblyNames = names.ToArray();
        OnPropertyChanged(nameof(AssemblyNames));
        if (!ContainsAssemblyName(mAssemblyNames, AssemblyName))
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
