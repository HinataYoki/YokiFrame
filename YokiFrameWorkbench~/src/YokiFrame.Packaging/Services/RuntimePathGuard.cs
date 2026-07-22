namespace YokiFrame.Packaging.Services;

/// <summary>
/// 统一约束项目 Runtime 缓存平台目录和 manifest 相对入口，防止路径逃逸缓存根。
/// </summary>
internal static class RuntimePathGuard
{
    /// <summary>
    /// 将平台标识解析为 Runtime 根下的直接子目录；平台名不能携带任何目录语义。
    /// </summary>
    /// <param name="runtimeRoot">项目级 Runtime 缓存根目录。</param>
    /// <param name="platform">待校验的平台标识。</param>
    /// <returns>平台目录完整路径。</returns>
    internal static string RequirePlatformRoot(string runtimeRoot, string platform)
    {
        if (string.IsNullOrWhiteSpace(platform)
            || !string.Equals(platform, platform.Trim(), StringComparison.Ordinal)
            || IsPortableRooted(platform)
            || platform is "." or ".."
            || platform.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException("Runtime platform must be a single relative directory name.", nameof(platform));
        }

        var fullRoot = Path.GetFullPath(runtimeRoot);
        return RequirePathInside(fullRoot, platform, nameof(platform));
    }

    /// <summary>
    /// 将 manifest 入口解析为指定平台目录内的文件路径，拒绝绝对路径和目录穿越。
    /// </summary>
    /// <param name="platformRoot">当前平台目录。</param>
    /// <param name="entry">manifest 相对入口。</param>
    /// <param name="parameterName">异常中使用的参数名。</param>
    /// <returns>入口完整路径。</returns>
    internal static string RequireEntryPath(string platformRoot, string entry, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(entry) || IsPortableRooted(entry))
        {
            throw new ArgumentException("Runtime entry must be a relative file path.", parameterName);
        }

        return RequirePathInside(Path.GetFullPath(platformRoot), entry, parameterName);
    }

    /// <summary>
    /// 尝试把已有 manifest 路径解析到 Runtime 根内；非法历史记录由调用方直接丢弃。
    /// </summary>
    /// <param name="runtimeRoot">项目级 Runtime 缓存根目录。</param>
    /// <param name="relativePath">已有 manifest 相对路径。</param>
    /// <param name="fullPath">合法时返回完整路径。</param>
    /// <returns>路径非空、非绝对且仍位于 Runtime 根内时返回 true。</returns>
    internal static bool TryResolveInside(string runtimeRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || IsPortableRooted(relativePath))
        {
            return false;
        }

        var fullRoot = Path.GetFullPath(runtimeRoot);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, NormalizeForHost(relativePath)));
        if (!IsInside(fullRoot, candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// 将 manifest 输出限制为 Runtime 根内唯一的 `tool-manifest.json`，拒绝任意文件覆盖。
    /// </summary>
    /// <param name="runtimeRoot">项目级 Runtime 缓存根目录。</param>
    /// <param name="outputPath">命令请求的 manifest 输出路径。</param>
    /// <returns>规范化后的固定 manifest 路径。</returns>
    internal static string RequireManifestPath(string runtimeRoot, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Runtime manifest output path is required.", nameof(outputPath));
        }

        var fullRoot = Path.GetFullPath(runtimeRoot);
        var expectedPath = Path.Combine(fullRoot, "tool-manifest.json");
        var fullOutputPath = Path.GetFullPath(outputPath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(expectedPath, fullOutputPath, comparison))
        {
            throw new ArgumentException(
                "Runtime manifest output must be runtimeRoot/tool-manifest.json.",
                nameof(outputPath));
        }

        return fullOutputPath;
    }

    /// <summary>
    /// 解析并确认相对路径仍在根目录内部；根目录本身不能作为文件或平台入口。
    /// </summary>
    /// <param name="fullRoot">已规范化根目录。</param>
    /// <param name="relativePath">待解析相对路径。</param>
    /// <param name="parameterName">异常参数名。</param>
    /// <returns>根目录内的完整路径。</returns>
    private static string RequirePathInside(string fullRoot, string relativePath, string parameterName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, NormalizeForHost(relativePath)));
        if (!IsInside(fullRoot, fullPath))
        {
            throw new ArgumentException("Runtime path escapes its allowed root.", parameterName);
        }

        return fullPath;
    }

    /// <summary>
    /// 使用宿主匹配规则检查候选路径是否为根目录的真实后代。
    /// </summary>
    /// <param name="fullRoot">已规范化根目录。</param>
    /// <param name="fullPath">已规范化候选路径。</param>
    /// <returns>候选路径位于根目录下时返回 true。</returns>
    private static bool IsInside(string fullRoot, string fullPath)
    {
        var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(rootPrefix, comparison);
    }

    /// <summary>
    /// 同时识别当前宿主和其它支持平台的绝对路径写法，避免跨平台 manifest 绕过校验。
    /// </summary>
    /// <param name="path">待检查路径。</param>
    /// <returns>路径具有根目录、UNC 或盘符语义时返回 true。</returns>
    private static bool IsPortableRooted(string path)
    {
        return Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal)
            || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':');
    }

    /// <summary>
    /// 将两类目录分隔符都转换为当前宿主分隔符，确保跨平台输入执行同一穿越判定。
    /// </summary>
    /// <param name="path">相对路径。</param>
    /// <returns>当前宿主可解析的相对路径。</returns>
    private static string NormalizeForHost(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}
