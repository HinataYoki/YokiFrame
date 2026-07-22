namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 提供 Project Model 扫描与文档生成共用的词法 containment 和重解析点检查。
/// </summary>
internal static class ProjectModelPathPolicy
{
    /// <summary>
    /// 判断候选路径是否等于指定根或位于根目录内部。
    /// </summary>
    /// <param name="root">受控根目录。</param>
    /// <param name="path">候选文件或目录。</param>
    /// <returns>候选未通过父目录或绝对路径逃逸时返回 true。</returns>
    internal static bool IsInsideOrSame(string root, string path)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !Path.IsPathRooted(relativePath)
            && relativePath != ".."
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断路径是否使用当前平台或其它受支持平台的绝对路径表达。
    /// </summary>
    /// <param name="path">待检查路径。</param>
    /// <returns>路径具有根、UNC 或盘符语义时返回 true。</returns>
    internal static bool IsPortableRooted(string path)
    {
        return Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal)
            || path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    /// <summary>
    /// 检查受控根及其到目标的现存路径链是否包含符号链接、Junction 或其它重解析点。
    /// </summary>
    /// <param name="root">已完成词法 containment 校验的受控根。</param>
    /// <param name="path">根内候选路径。</param>
    /// <returns>任一现存路径组件是重解析点时返回 true。</returns>
    internal static bool ContainsReparsePoint(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        if (TryGetAttributes(fullRoot, out var rootAttributes)
            && (rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }

        var current = fullRoot;
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        foreach (var segment in relativePath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!TryGetAttributes(current, out var attributes))
            {
                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 尝试读取现存路径组件属性；不存在的后续目标由调用方按自己的缺失契约处理。
    /// </summary>
    /// <param name="path">待检查文件系统路径。</param>
    /// <param name="attributes">路径存在时返回其属性。</param>
    /// <returns>属性读取成功时返回 true。</returns>
    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
    }
}
