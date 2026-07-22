namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 为 Workbench 契约测试定位源码文件与工具链根目录，统一处理测试输出目录差异。
/// </summary>
internal static class WorkbenchContractTestFiles
{
    /// <summary>
    /// 从测试输出目录向上查找 Workbench 项目内的指定源码文件。
    /// </summary>
    /// <param name="segments">Workbench Avalonia 项目内的相对路径片段。</param>
    /// <returns>目标源码文件的完整文本。</returns>
    /// <exception cref="FileNotFoundException">向上遍历后仍找不到目标文件时抛出。</exception>
    internal static string ReadSource(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var direct = BuildPath(directory.FullName, ["src", "YokiFrame.Workbench.Avalonia"], segments);
            if (File.Exists(direct))
            {
                return File.ReadAllText(direct);
            }

            var workspace = BuildPath(
                directory.FullName,
                ["Assets", "YokiFrame", "YokiFrameWorkbench~", "src", "YokiFrame.Workbench.Avalonia"],
                segments);
            if (File.Exists(workspace))
            {
                return File.ReadAllText(workspace);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Workbench 页面文件: " + string.Join("/", segments));
    }

    /// <summary>
    /// 从测试输出目录向上定位 YokiFrameWorkbench~ 根目录。
    /// </summary>
    /// <returns>YokiFrameWorkbench~ 的绝对路径。</returns>
    /// <exception cref="DirectoryNotFoundException">向上遍历后仍找不到工具链根目录时抛出。</exception>
    internal static string FindWorkbenchRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "YokiFrame.Workbench.Avalonia")))
            {
                return directory.FullName;
            }

            var nested = Path.Combine(directory.FullName, "Assets", "YokiFrame", "YokiFrameWorkbench~");
            if (Directory.Exists(Path.Combine(nested, "src", "YokiFrame.Workbench.Avalonia")))
            {
                return nested;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 根目录。");
    }

    /// <summary>
    /// 将固定前缀与测试传入的相对片段组合为平台规范路径。
    /// </summary>
    /// <param name="root">候选项目根目录。</param>
    /// <param name="prefix">从候选根到 Workbench 项目的固定路径。</param>
    /// <param name="segments">目标文件在 Workbench 项目内的相对路径。</param>
    /// <returns>组合后的候选绝对路径。</returns>
    private static string BuildPath(string root, string[] prefix, string[] segments)
    {
        var parts = new string[1 + prefix.Length + segments.Length];
        parts[0] = root;
        prefix.CopyTo(parts, 1);
        segments.CopyTo(parts, 1 + prefix.Length);
        return Path.Combine(parts);
    }
}
