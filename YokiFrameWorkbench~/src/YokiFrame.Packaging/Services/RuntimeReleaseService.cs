namespace YokiFrame.Packaging.Services;

/// <summary>
/// 验证 Git URL 源码发布包不携带任何可再生 Workbench Runtime 产物。
/// </summary>
public sealed class RuntimeReleaseService
{
    private const string RUNTIME_ROOT_NAME = "WorkbenchRuntime~";
    private const string WORKBENCH_ROOT_NAME = "YokiFrameWorkbench~";

    private static readonly string[] sBootstrapEntryNames =
    {
        "build-current-platform.cmd",
        "build-current-platform.sh",
        "build-current-platform.command",
        "install-godot.cmd",
        "install-godot.sh",
        "install-godot.command"
    };

    private static readonly string[] sRequiredTrackedPaths = CreateRequiredTrackedPaths();

    private readonly GitIndexReleaseReader mGitIndexReader = new();

    /// <summary>
    /// 执行无需 Git index 的源码包预检；该命令不生成 manifest、bootstrap 副本或二进制。
    /// </summary>
    /// <param name="packageRoot">独立 YokiFrame 包根。</param>
    public void Prepare(string packageRoot)
    {
        var fullPackageRoot = RequirePackageRoot(packageRoot);
        ValidateSourceBootstrapTemplates(fullPackageRoot);
        ValidateNoRuntimePayloadOnDisk(fullPackageRoot);
    }

    /// <summary>
    /// 验证 Git index 和工作树均未携带 Runtime、缓存或普通编译产物。
    /// </summary>
    /// <param name="packageRoot">独立 YokiFrame 包根。</param>
    public void Verify(string packageRoot)
    {
        var fullPackageRoot = RequirePackageRoot(packageRoot);
        Prepare(fullPackageRoot);
        mGitIndexReader.EnsureRepositoryRoot(fullPackageRoot);
        var trackedPaths = mGitIndexReader.ReadTrackedPaths(fullPackageRoot);
        ValidateRequiredTrackedPaths(trackedPaths);
        foreach (var path in trackedPaths)
        {
            if (IsForbiddenTrackedPath(path))
            {
                throw new InvalidDataException("Tracked source-release path is not allowed: " + path);
            }
        }

        mGitIndexReader.EnsureWorkingTreeMatchesIndex(fullPackageRoot);
        var untrackedPaths = mGitIndexReader.ReadUntrackedPaths(fullPackageRoot);
        if (untrackedPaths.Count > 0)
        {
            throw new InvalidDataException(
                "Untracked source-release path is not included in the Git index: " + untrackedPaths[0]);
        }
    }

    /// <summary>
    /// 验证包根存在，并返回规范化完整路径。
    /// </summary>
    /// <param name="packageRoot">待验证的 YokiFrame 包根。</param>
    /// <returns>规范化完整包根。</returns>
    private static string RequirePackageRoot(string packageRoot)
    {
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            throw new ArgumentException("Package root is required.", nameof(packageRoot));
        }

        var fullPackageRoot = Path.GetFullPath(packageRoot);
        return Directory.Exists(fullPackageRoot)
            ? fullPackageRoot
            : throw new DirectoryNotFoundException("YokiFrame package root was not found: " + fullPackageRoot);
    }

    /// <summary>
    /// 验证源码包保留三种通用 bootstrap 与三种 Godot 安装入口模板，用户从这些模板生成项目缓存而非使用包内副本。
    /// </summary>
    /// <param name="packageRoot">已验证的 YokiFrame 包根。</param>
    private static void ValidateSourceBootstrapTemplates(string packageRoot)
    {
        var bootstrapRoot = Path.Combine(packageRoot, WORKBENCH_ROOT_NAME, "scripts", "runtime-bootstrap");
        foreach (var entryName in sBootstrapEntryNames)
        {
            var path = Path.Combine(bootstrapRoot, entryName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Source Runtime bootstrap template is missing.", path);
            }
        }
    }

    /// <summary>
    /// 拒绝包根内残留的 Runtime profile、manifest、staging 或锁，防止忽略规则掩盖错误发布流程。
    /// </summary>
    /// <param name="packageRoot">已验证的 YokiFrame 包根。</param>
    private static void ValidateNoRuntimePayloadOnDisk(string packageRoot)
    {
        var runtimeRoot = Path.Combine(packageRoot, RUNTIME_ROOT_NAME);
        if (Directory.Exists(runtimeRoot)
            && Directory.EnumerateFileSystemEntries(runtimeRoot, "*", SearchOption.AllDirectories).Any())
        {
            throw new InvalidDataException("Source release package must not contain WorkbenchRuntime~ payloads: " + runtimeRoot);
        }
    }

    /// <summary>
    /// 验证包身份与所有 bootstrap 权威模板已经进入 Git index，避免空索引或被 ignore 的必需文件假阳性通过。
    /// </summary>
    /// <param name="trackedPaths">当前 Git index 路径。</param>
    private static void ValidateRequiredTrackedPaths(IReadOnlyList<string> trackedPaths)
    {
        if (trackedPaths.Count == 0)
        {
            throw new InvalidDataException("YokiFrame Git index is empty; stage the source release before verification.");
        }

        HashSet<string> trackedPathSet = new(trackedPaths, StringComparer.Ordinal);
        foreach (var requiredPath in sRequiredTrackedPaths)
        {
            if (!trackedPathSet.Contains(requiredPath))
            {
                throw new InvalidDataException(
                    "Required source-release file is not tracked in the Git index: " + requiredPath);
            }
        }
    }

    /// <summary>
    /// 创建必须进入源码发布 index 的稳定入口列表；实际源码遗漏由未跟踪文件门禁继续覆盖。
    /// </summary>
    /// <returns>包元数据与 bootstrap 模板路径。</returns>
    private static string[] CreateRequiredTrackedPaths()
    {
        var paths = new string[sBootstrapEntryNames.Length + 1];
        paths[0] = "package.json";
        for (var index = 0; index < sBootstrapEntryNames.Length; index++)
        {
            paths[index + 1] = WORKBENCH_ROOT_NAME
                + "/scripts/runtime-bootstrap/"
                + sBootstrapEntryNames[index];
        }

        return paths;
    }

    /// <summary>
    /// 判断 Git 路径是否属于可再生产物、缓存或历史发布目录。
    /// </summary>
    /// <param name="path">使用正斜杠的 Git 相对路径。</param>
    /// <returns>不允许进入源码发布包时返回 true。</returns>
    private static bool IsForbiddenTrackedPath(string path)
    {
        return path.StartsWith(RUNTIME_ROOT_NAME + "/", StringComparison.Ordinal)
            || path.StartsWith(".yokiframe/", StringComparison.Ordinal)
            || path.StartsWith(WORKBENCH_ROOT_NAME + "/.artifacts/", StringComparison.Ordinal)
            || path.StartsWith("TauriRuntime~/", StringComparison.Ordinal)
            || path.StartsWith("ToolRuntime~/", StringComparison.Ordinal)
            || HasDirectorySegment(path, "bin")
            || HasDirectorySegment(path, "obj")
            || IsCompiledArtifact(path);
    }

    /// <summary>
    /// 判断 Git 路径是否包含指定目录片段，避免按普通文件名子串误伤。
    /// </summary>
    /// <param name="path">使用正斜杠的 Git 相对路径。</param>
    /// <param name="directoryName">待排除目录名称。</param>
    /// <returns>存在完整目录片段时返回 true。</returns>
    private static bool HasDirectorySegment(string path, string directoryName)
    {
        return path.StartsWith(directoryName + "/", StringComparison.Ordinal)
            || path.Contains("/" + directoryName + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断路径是否为普通 .NET 或 Native AOT 编译产物；源码配置文件不在此集合内。
    /// </summary>
    /// <param name="path">使用正斜杠的 Git 相对路径。</param>
    /// <returns>属于编译产物时返回 true。</returns>
    private static bool IsCompiledArtifact(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pdb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".so", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".dylib", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
    }
}
