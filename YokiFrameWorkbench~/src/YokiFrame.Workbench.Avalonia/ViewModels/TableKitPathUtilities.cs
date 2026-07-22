namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>集中处理 TableKit 页面路径的相对显示、绝对解析和目录选择器起点。</summary>
internal static class TableKitPathUtilities
{
    /// <summary>把路径解析为基于项目根的绝对路径。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="path">绝对路径或项目相对路径。</param>
    /// <returns>规范化绝对路径；空输入返回空字符串。</returns>
    internal static string Resolve(string projectRoot, string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(projectRoot, path));
    }

    /// <summary>把路径转换为项目相对显示形式，避免界面暴露冗长的项目根前缀。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="path">绝对路径或项目相对路径。</param>
    /// <returns>使用正斜杠的项目相对路径；跨卷路径保持绝对形式。</returns>
    internal static string ToRelative(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string root = Path.GetFullPath(projectRoot);
        string full = Resolve(root, path);
        string relative = Path.GetRelativePath(root, full);
        return Path.IsPathFullyQualified(relative)
            ? full
            : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>定位目录选择器首页；当前路径不存在时逐级回退到最近存在的父目录。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="configuredPath">字段当前显示的路径。</param>
    /// <param name="isFilePath">当前字段是否指向文件而非目录。</param>
    /// <returns>可供原生目录选择器使用的已存在绝对目录。</returns>
    internal static string FindPickerStartDirectory(string projectRoot, string configuredPath, bool isFilePath)
    {
        string root = Path.GetFullPath(projectRoot);
        string resolved = Resolve(root, configuredPath);
        string? candidate = string.IsNullOrWhiteSpace(resolved)
            ? root
            : isFilePath ? Path.GetDirectoryName(resolved) : resolved;
        while (!string.IsNullOrWhiteSpace(candidate) && !Directory.Exists(candidate))
        {
            candidate = Path.GetDirectoryName(candidate);
        }

        return string.IsNullOrWhiteSpace(candidate) ? root : candidate;
    }
}
