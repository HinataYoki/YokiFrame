using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FileBridge.IO;

/// <summary>
/// 提供路径归一化和根目录 containment 检查，防止 FileBridge 访问越界。
/// </summary>
internal static class PathSecurity
{
    /// <summary>
    /// 合并路径片段并确认结果仍在指定根目录内。
    /// </summary>
    /// <param name="rootPath">允许访问的根目录。</param>
    /// <param name="segments">待合并的路径片段。</param>
    /// <returns>已归一化的完整路径。</returns>
    public static string CombineInside(string rootPath, params string[] segments)
    {
        var combinedPath = rootPath;
        foreach (var seg in segments) combinedPath = Path.Combine(combinedPath, seg);
        var fullPath = EnsureInside(rootPath, combinedPath);
        EnsureNoReparsePoint(rootPath, fullPath);
        return fullPath;
    }

    /// <summary>
    /// 拒绝受控根及其到目标的现存路径链包含符号链接、Junction 或其它重解析点。
    /// </summary>
    /// <param name="rootPath">受控根目录。</param>
    /// <param name="candidatePath">已位于根内的候选路径。</param>
    public static void EnsureNoReparsePoint(string rootPath, string candidatePath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        var fullCandidate = EnsureInside(fullRoot, candidatePath);
        var current = fullRoot;
        EnsurePathComponentIsNotReparsePoint(current, fullCandidate);
        var relativePath = Path.GetRelativePath(fullRoot, fullCandidate);
        foreach (var segment in relativePath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            EnsurePathComponentIsNotReparsePoint(current, fullCandidate);
        }
    }

    /// <summary>
    /// 确认候选路径位于根目录内；失败时抛出 PathTraversalRejected。
    /// </summary>
    /// <param name="rootPath">允许访问的根目录。</param>
    /// <param name="candidatePath">待检查路径。</param>
    /// <returns>已归一化的候选路径。</returns>
    public static string EnsureInside(string rootPath, string candidatePath)
    {
        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        var fullCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (fullCandidate.StartsWith(fullRoot, comparison)
            || string.Equals(RemoveTrailingSeparator(fullCandidate), RemoveTrailingSeparator(fullRoot), comparison))
        {
            return fullCandidate;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "PathTraversalRejected",
            $"Path is outside allowed root: {fullCandidate}",
            "Use a project-local FileBridge path and avoid '..' or absolute child arguments.",
            new[] { fullRoot, fullCandidate }));
    }

    /// <summary>
    /// 给目录路径补齐结尾分隔符，避免 sibling prefix 绕过 containment 检查。
    /// </summary>
    /// <param name="path">待处理路径。</param>
    /// <returns>结尾带目录分隔符的路径。</returns>
    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// 移除路径结尾分隔符，用于根目录自身的等值判断。
    /// </summary>
    /// <param name="path">待处理路径。</param>
    /// <returns>去掉结尾分隔符后的路径。</returns>
    private static string RemoveTrailingSeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 校验单个现存路径组件不是重解析点，并映射为稳定协议错误。
    /// </summary>
    /// <param name="path">待检查路径组件。</param>
    /// <param name="candidatePath">完整候选路径，用于错误证据。</param>
    private static void EnsurePathComponentIsNotReparsePoint(string path, string candidatePath)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "PathReparsePointRejected",
            $"FileBridge path contains a symbolic link or junction: {path}",
            "Replace linked FileBridge directories with ordinary project-local directories.",
            new[] { path, candidatePath }));
    }
}
