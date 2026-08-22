using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FileBridge.IO;

/// <summary>
/// 提供路径归一化和根目录 containment 检查，防止 FileBridge 访问越界。
/// 扫描机制单源复用共享的 <see cref="YokiFrameFilePathPolicy"/>；本类型只负责把
/// 共享 IOException 转换为带稳定错误码（PathTraversalRejected / PathReparsePointRejected）的协议异常。
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
        foreach (var segment in segments)
        {
            combinedPath = Path.Combine(combinedPath, segment);
        }

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
        try
        {
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(rootPath, candidatePath);
        }
        catch (IOException exception)
        {
            throw CreateReparsePointRejected(exception);
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
        try
        {
            return YokiFrameFilePathPolicy.EnsureInside(rootPath, candidatePath);
        }
        catch (IOException exception)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "PathTraversalRejected",
                exception.Message,
                "Use a project-local FileBridge path and avoid '..' or absolute child arguments.",
                new[] { rootPath, candidatePath }));
        }
    }

    /// <summary>把共享扫描抛出的 IOException 统一映射为 PathReparsePointRejected 协议异常。</summary>
    private static YokiFrameProtocolException CreateReparsePointRejected(IOException exception)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            "PathReparsePointRejected",
            exception.Message,
            "Replace linked FileBridge directories with ordinary project-local directories.",
            Array.Empty<string>()));
    }
}
