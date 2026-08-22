namespace YokiFrame.RuntimeCache;

/// <summary>
/// 统一 Runtime manifest 的跨平台路径 containment、载荷过滤与符号链接策略。
/// </summary>
public static class RuntimeManifestPathPolicy
{
    private const string RUNTIME_STATE_DIRECTORY_NAME = ".yokiframe";

    /// <summary>获取与当前宿主文件系统一致的路径集合比较器。</summary>
    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 将相对目录解析到指定根内，并拒绝不存在或包含符号链接的目录链。
    /// </summary>
    /// <param name="root">约束根目录。</param>
    /// <param name="relativePath">相对目录路径。</param>
    /// <param name="fullPath">可信目录完整路径。</param>
    /// <returns>目录存在、位于根内且目录链无 reparse point 时返回 true。</returns>
    public static bool TryResolveDirectoryInside(string root, string relativePath, out string fullPath)
    {
        return TryResolveInside(root, relativePath, out fullPath)
            && Directory.Exists(fullPath)
            && !ContainsReparsePoint(root, fullPath);
    }

    /// <summary>
    /// 将相对文件解析到指定根内，并拒绝不存在或包含符号链接的文件链。
    /// </summary>
    /// <param name="root">约束根目录。</param>
    /// <param name="relativePath">相对文件路径。</param>
    /// <param name="fullPath">可信文件完整路径。</param>
    /// <returns>文件存在、位于根内且路径链无 reparse point 时返回 true。</returns>
    public static bool TryResolveFileInside(string root, string relativePath, out string fullPath)
    {
        return TryResolveInside(root, relativePath, out fullPath)
            && File.Exists(fullPath)
            && !ContainsReparsePoint(root, fullPath);
    }

    /// <summary>
    /// 判断候选文件是否为需要进入 manifest 的发布载荷。
    /// </summary>
    /// <param name="platformRoot">平台根目录。</param>
    /// <param name="path">候选文件路径。</param>
    /// <returns>非调试符号且不位于平台内运行态目录时返回 true。</returns>
    public static bool IsRuntimePayloadFile(string platformRoot, string path)
    {
        return !string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase)
            && !ContainsRelativeDirectory(platformRoot, path, RUNTIME_STATE_DIRECTORY_NAME);
    }

    /// <summary>
    /// 判断目录是否为平台内部允许忽略的运行态状态目录。
    /// </summary>
    /// <param name="path">候选目录。</param>
    /// <returns>目录名为 `.yokiframe` 时返回 true。</returns>
    public static bool IsRuntimeStateDirectory(string path)
    {
        return string.Equals(
            Path.GetFileName(Path.TrimEndingDirectorySeparator(path)),
            RUNTIME_STATE_DIRECTORY_NAME,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断完整路径是否为指定根目录的后代。
    /// </summary>
    /// <param name="root">根目录。</param>
    /// <param name="path">候选完整路径。</param>
    /// <returns>候选位于根目录内时返回 true。</returns>
    public static bool IsInside(string root, string path)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, GetPathComparison());
    }

    /// <summary>
    /// 判断文件系统项是否为符号链接、junction 或其它 reparse point。
    /// </summary>
    /// <param name="path">文件或目录完整路径。</param>
    /// <returns>文件系统属性包含 ReparsePoint 时返回 true。</returns>
    public static bool IsReparsePoint(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    /// <summary>
    /// 将跨平台相对路径解析到指定根内，拒绝绝对路径和目录穿越。
    /// </summary>
    /// <param name="root">约束根目录。</param>
    /// <param name="relativePath">manifest 相对路径。</param>
    /// <param name="fullPath">合法时返回完整路径。</param>
    /// <returns>路径位于根目录后代时返回 true。</returns>
    private static bool TryResolveInside(string root, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || IsPortableRooted(relativePath))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, NormalizeForHost(relativePath)));
        if (!IsInside(root, candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// 检查从 Runtime 根到目标项的每一级是否包含 reparse point，防止词法路径位于根内但实际指向根外。
    /// </summary>
    /// <param name="root">Runtime 根目录。</param>
    /// <param name="path">根内已存在的目标路径。</param>
    /// <returns>任一级为链接或 junction 时返回 true。</returns>
    private static bool ContainsReparsePoint(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        if (IsReparsePoint(fullRoot))
        {
            return true;
        }

        var current = fullRoot;
        var relativePath = Path.GetRelativePath(fullRoot, Path.GetFullPath(path));
        foreach (var segment in relativePath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断文件相对平台根的祖先目录是否包含指定名称，不检查平台根之外的目录。
    /// </summary>
    /// <param name="platformRoot">平台根目录。</param>
    /// <param name="path">平台内文件。</param>
    /// <param name="directoryName">待匹配目录名。</param>
    /// <returns>相对祖先目录匹配时返回 true。</returns>
    private static bool ContainsRelativeDirectory(string platformRoot, string path, string directoryName)
    {
        var relativePath = Path.GetRelativePath(platformRoot, path).Replace('\\', '/');
        var lastSeparator = relativePath.LastIndexOf('/');
        if (lastSeparator < 0)
        {
            return false;
        }

        return relativePath[..lastSeparator]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, directoryName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 识别当前宿主和其它支持平台的绝对路径写法。
    /// </summary>
    /// <param name="path">待检查路径。</param>
    /// <returns>具有根目录、UNC 或盘符语义时返回 true。</returns>
    private static bool IsPortableRooted(string path)
    {
        return Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal)
            || path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    /// <summary>
    /// 将两类分隔符转换为当前宿主分隔符。
    /// </summary>
    /// <param name="path">跨平台相对路径。</param>
    /// <returns>当前宿主可解析路径。</returns>
    private static string NormalizeForHost(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// 获取当前文件系统的路径比较规则。
    /// </summary>
    /// <returns>Windows 忽略大小写，其它宿主区分大小写。</returns>
    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}
