namespace YokiFrame.Tooling.Application.Documentation.Internal;

/// <summary>
/// 约束离线文档只能来自传入 YokiFrame 包根内、供 Workbench 公开的文档目录。
/// </summary>
internal sealed class DocumentationPathPolicy
{
    private const string PACKAGE_DOCUMENTATION_PREFIX = "Documentation~";
    private const string PUBLIC_API_DIRECTORY = "Api";
    private const string PUBLIC_GUIDES_DIRECTORY = "Guides";
    private const string DOCUMENT_CONTINUATION_SUFFIX = ".part.md";

    private readonly string mPackageDocumentationRoot;
    private readonly string mPublicApiDocumentationRoot;
    private readonly string mPublicGuidesDocumentationRoot;

    /// <summary>
    /// 规范化直接传入的 YokiFrame 包根，不推断项目 Assets 布局。
    /// </summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    internal DocumentationPathPolicy(string packageRoot)
    {
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            throw new ArgumentException("YokiFrame 包根不能为空。", nameof(packageRoot));
        }

        PackageRoot = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(PackageRoot))
        {
            throw new DirectoryNotFoundException($"YokiFrame 包根不存在: {PackageRoot}");
        }

        mPackageDocumentationRoot = Path.Combine(PackageRoot, PACKAGE_DOCUMENTATION_PREFIX);
        mPublicApiDocumentationRoot = Path.Combine(mPackageDocumentationRoot, PUBLIC_API_DIRECTORY);
        mPublicGuidesDocumentationRoot = Path.Combine(mPackageDocumentationRoot, PUBLIC_GUIDES_DIRECTORY);
    }

    /// <summary>
    /// 获取已规范化的 YokiFrame 包根。
    /// </summary>
    internal string PackageRoot { get; }

    /// <summary>
    /// 枚举面向用户的 API 与指南 Markdown，避免把 ADR、迁移底稿和内部开发资料暴露到 Workbench。
    /// </summary>
    /// <returns>按公开相对路径排序的安全文档位置。</returns>
    internal IReadOnlyList<DocumentationFileLocation> EnumerateMarkdownFiles()
    {
        var locations = new List<DocumentationFileLocation>();
        AddRootFiles(
            locations,
            mPublicApiDocumentationRoot,
            PACKAGE_DOCUMENTATION_PREFIX + "/" + PUBLIC_API_DIRECTORY,
            DocumentationSourceKind.PackageDocumentation);
        AddRootFiles(
            locations,
            mPublicGuidesDocumentationRoot,
            PACKAGE_DOCUMENTATION_PREFIX + "/" + PUBLIC_GUIDES_DIRECTORY,
            DocumentationSourceKind.PackageDocumentation);
        return locations
            .OrderBy(static item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 在实际全文读取前重新验证受控根与所有包内路径组件，避免扫描后的重解析点替换。
    /// </summary>
    /// <param name="location">此前已由本策略解析或枚举的文档位置。</param>
    /// <returns>Markdown 完整正文。</returns>
    internal string ReadMarkdown(DocumentationFileLocation location)
    {
        var resolvedLocation = ResolveDocument(location.RelativePath);
        var markdown = File.ReadAllText(resolvedLocation.FullPath);
        var continuationPath = Path.Combine(
            Path.GetDirectoryName(resolvedLocation.FullPath)!,
            Path.GetFileNameWithoutExtension(resolvedLocation.FullPath) + DOCUMENT_CONTINUATION_SUFFIX);
        if (!File.Exists(continuationPath))
        {
            return markdown;
        }

        EnsureSafeMarkdownPath(mPackageDocumentationRoot, continuationPath);
        return markdown.TrimEnd() + System.Environment.NewLine + System.Environment.NewLine
            + File.ReadAllText(continuationPath).TrimStart();
    }

    /// <summary>
    /// 把调用方提供的包内相对路径解析为受控 Markdown 位置。
    /// </summary>
    /// <param name="relativePath">相对 YokiFrame 包根的路径。</param>
    /// <returns>已完成 containment 校验的位置。</returns>
    internal DocumentationFileLocation ResolveDocument(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("文档相对路径不能为空。", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new UnauthorizedAccessException("文档路径必须相对 YokiFrame 包根。");
        }

        var normalizedPath = NormalizeRelativePath(relativePath);
        if (TryResolveRoot(normalizedPath, out var location))
        {
            return location;
        }

        throw new UnauthorizedAccessException("文档路径不属于受控 Documentation 根。");
    }

    /// <summary>
    /// 收集单个受控根中的 Markdown 文件。
    /// </summary>
    /// <param name="locations">结果集合。</param>
    /// <param name="root">受控绝对根。</param>
    /// <param name="prefix">公开相对路径前缀。</param>
    /// <param name="sourceKind">受控根分类。</param>
    private void AddRootFiles(
        ICollection<DocumentationFileLocation> locations,
        string root,
        string prefix,
        DocumentationSourceKind sourceKind)
    {
        EnsureNoReparsePoint(PackageRoot, Path.GetRelativePath(PackageRoot, root));
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var path in EnumerateMarkdownPaths(root))
        {
            var fullPath = Path.GetFullPath(path);
            EnsureSafeMarkdownPath(root, fullPath);
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            locations.Add(new DocumentationFileLocation(
                fullPath,
                prefix + "/" + relative,
                sourceKind));
        }
    }

    /// <summary>
    /// 使用不跟随重解析点的选项枚举 Markdown。
    /// </summary>
    /// <param name="root">受控绝对根。</param>
    /// <returns>根内 Markdown 路径序列。</returns>
    private static IEnumerable<string> EnumerateMarkdownPaths(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.PlatformDefault,
        };
        return Directory.EnumerateFiles(root, "*.md", options)
            .Where(static path => !path.EndsWith(DOCUMENT_CONTINUATION_SUFFIX, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 识别 Workbench 公开文档前缀并在对应物理根内执行 containment 校验。
    /// </summary>
    /// <param name="relativePath">已规范化包内相对路径。</param>
    /// <param name="location">成功时返回安全位置。</param>
    /// <returns>路径是否属于受控根。</returns>
    private bool TryResolveRoot(string relativePath, out DocumentationFileLocation location)
    {
        if (TryResolveUnderRoot(
                relativePath,
                PACKAGE_DOCUMENTATION_PREFIX + "/" + PUBLIC_API_DIRECTORY,
                mPublicApiDocumentationRoot,
                DocumentationSourceKind.PackageDocumentation,
                out location))
        {
            return true;
        }

        return TryResolveUnderRoot(
            relativePath,
            PACKAGE_DOCUMENTATION_PREFIX + "/" + PUBLIC_GUIDES_DIRECTORY,
            mPublicGuidesDocumentationRoot,
            DocumentationSourceKind.PackageDocumentation,
            out location);
    }

    /// <summary>
    /// 在指定受控根内解析路径，并拒绝父目录、非 Markdown 与重解析逃逸。
    /// </summary>
    /// <param name="relativePath">包内相对路径。</param>
    /// <param name="prefix">受控公开前缀。</param>
    /// <param name="root">受控物理根。</param>
    /// <param name="sourceKind">受控根分类。</param>
    /// <param name="location">成功时返回安全位置。</param>
    /// <returns>路径前缀是否匹配当前根。</returns>
    private bool TryResolveUnderRoot(
        string relativePath,
        string prefix,
        string root,
        DocumentationSourceKind sourceKind,
        out DocumentationFileLocation location)
    {
        location = null!;
        if (!relativePath.StartsWith(prefix + "/", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = relativePath[(prefix.Length + 1)..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, suffix));
        EnsureSafeMarkdownPath(root, fullPath);
        location = new DocumentationFileLocation(fullPath, relativePath, sourceKind);
        return true;
    }

    /// <summary>
    /// 校验物理路径仍在受控根内、扩展名正确且不存在重解析组件。
    /// </summary>
    /// <param name="root">受控物理根。</param>
    /// <param name="fullPath">待读取绝对路径。</param>
    private void EnsureSafeMarkdownPath(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("文档路径越过受控根。");
        }

        if (!Path.GetExtension(fullPath).Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("离线文档读取仅允许 Markdown 文件。");
        }

        var packageRelative = Path.GetRelativePath(PackageRoot, fullPath);
        EnsureNoReparsePoint(PackageRoot, packageRelative);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("受控 Markdown 文档不存在。", fullPath);
        }
    }

    /// <summary>
    /// 拒绝由符号链接或目录联接把读取重定向到受控根外。
    /// </summary>
    /// <param name="root">受控物理根。</param>
    /// <param name="relativePath">相对受控根的路径。</param>
    private static void EnsureNoReparsePoint(string root, string relativePath)
    {
        var current = root;
        EnsurePathComponentIsNotReparsePoint(current);
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            EnsurePathComponentIsNotReparsePoint(current);
        }
    }

    /// <summary>
    /// 拒绝单个现存路径组件是重解析点；不存在的后续路径仍由文件缺失契约处理。
    /// </summary>
    /// <param name="path">待检查文件或目录。</param>
    private static void EnsurePathComponentIsNotReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException("受控文档路径不能包含重解析点。");
        }
    }

    /// <summary>
    /// 把调用方路径统一为包内斜杠表达。
    /// </summary>
    /// <param name="relativePath">调用方相对路径。</param>
    /// <returns>斜杠表达的相对路径。</returns>
    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Trim().Replace('\\', '/');
    }
}
