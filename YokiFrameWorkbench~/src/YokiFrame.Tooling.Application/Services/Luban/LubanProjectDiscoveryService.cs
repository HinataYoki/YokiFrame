using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Services.Luban;

/// <summary>按项目内常见 Luban 布局发现唯一配置、可执行文件和默认 target。</summary>
public sealed class LubanProjectDiscoveryService
{
    private const int MAX_DISCOVERED_PATH_COUNT = 8;
    private readonly LubanConfigurationReader mConfigurationReader;

    /// <summary>创建使用默认 luban.conf 读取器的发现服务。</summary>
    public LubanProjectDiscoveryService() : this(new LubanConfigurationReader())
    {
    }

    /// <summary>创建复用指定配置读取器的发现服务。</summary>
    /// <param name="configurationReader">解析 target、dataDir 和 schemaFiles 的中立读取器。</param>
    public LubanProjectDiscoveryService(LubanConfigurationReader configurationReader)
    {
        mConfigurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
    }

    /// <summary>自动发现项目内唯一可用的 Luban 工具参数；多份配置时拒绝猜测。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <returns>可直接调用的参数，或可操作的失败诊断。</returns>
    public LubanToolDiscoveryResult Discover(string projectRoot) => Discover(projectRoot, string.Empty);

    /// <summary>发现指定项目内的 Luban 工具参数；显式工作目录会覆盖自动配置扫描。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="lubanWorkDir">可选的项目内工作目录；为空时自动扫描常见配置路径。</param>
    /// <returns>可直接调用的参数，或可操作的失败诊断。</returns>
    public LubanToolDiscoveryResult Discover(string projectRoot, string lubanWorkDir)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return Failed("项目根不能为空。");
        }

        string root = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(root))
        {
            return Failed("项目根不存在: " + root);
        }

        IReadOnlyList<string> configPaths;
        if (string.IsNullOrWhiteSpace(lubanWorkDir))
        {
            configPaths = FindConfigPaths(root);
        }
        else
        {
            if (!TryResolveProjectWorkDirectory(root, lubanWorkDir, out string configuredDirectory, out string diagnostic))
            {
                return Failed(diagnostic);
            }

            if (!Directory.Exists(configuredDirectory))
            {
                return Failed("Luban 工作目录不存在: " + configuredDirectory);
            }

            string configPath = Path.Combine(configuredDirectory, "luban.conf");
            if (!File.Exists(configPath))
            {
                return Failed("Luban 工作目录未找到 luban.conf: " + configPath);
            }

            configPaths = new[] { configPath };
        }
        if (configPaths.Count == 0)
        {
            return Failed("未在项目根或 Luban 目录中找到唯一的 luban.conf。");
        }

        if (configPaths.Count > 1)
        {
            return Failed("发现多个 luban.conf，无法自动选择: " + string.Join("; ", configPaths));
        }

        try
        {
            LubanConfiguration configuration = mConfigurationReader.Read(configPaths[0]);
            string targetName = SelectTarget(configuration.TargetNames);
            if (targetName.Length == 0)
            {
                return Failed("luban.conf 没有可用于本地化预览的唯一 target。", configuration);
            }

            IReadOnlyList<string> executablePaths = FindExecutablePaths(root, configuration.ConfigDirectory);
            if (executablePaths.Count == 0)
            {
                return Failed("未找到 Luban.dll 或 Luban 可执行文件。", configuration);
            }

            if (executablePaths.Count > 1)
            {
                return Failed("发现多个 Luban 工具，无法自动选择: " + string.Join("; ", executablePaths), configuration);
            }

            string mainExecutablePath = executablePaths[0];
            return new LubanToolDiscoveryResult
            {
                Succeeded = true,
                Configuration = configuration,
                Options = new LubanToolOptions
                {
                    ProjectRoot = root,
                    LubanConfigPath = configuration.ConfigPath,
                    LubanWorkDir = configuration.ConfigDirectory,
                    LubanExecutablePath = mainExecutablePath,
                    LubanAgentExecutablePath = FindOptionalExecutablePath(
                        root,
                        configuration.ConfigDirectory,
                        mainExecutablePath,
                        "Luban.Agent",
                        "Luban.Agent"),
                    LubanMcpExecutablePath = FindOptionalExecutablePath(
                        root,
                        configuration.ConfigDirectory,
                        mainExecutablePath,
                        "Luban.Mcp",
                        "Luban.Mcp"),
                    LubanSkillsPath = FindOptionalSkillsPath(
                        root,
                        configuration.ConfigDirectory,
                        mainExecutablePath),
                    LubanDocumentationPath = FindOptionalDocumentationPath(
                        root,
                        configuration.ConfigDirectory,
                        mainExecutablePath),
                    TargetName = targetName
                }
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return Failed(exception.Message);
        }
    }

    /// <summary>寻找项目根与 Luban 目录内的配置文件，并限制扫描数量避免异常目录造成高成本遍历。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <returns>按路径排序且去重的配置文件列表。</returns>
    private static IReadOnlyList<string> FindConfigPaths(string projectRoot)
    {
        HashSet<string> paths = new(GetPathComparer());
        AddExistingFile(paths, Path.Combine(projectRoot, "luban.conf"));
        string lubanDirectory = Path.Combine(projectRoot, "Luban");
        AddExistingFile(paths, Path.Combine(lubanDirectory, "luban.conf"));
        if (Directory.Exists(lubanDirectory))
        {
            foreach (string path in Directory.EnumerateFiles(lubanDirectory, "luban.conf", SearchOption.AllDirectories)
                         .OrderBy(static value => value, GetPathComparer())
                         .Take(MAX_DISCOVERED_PATH_COUNT))
            {
                AddExistingFile(paths, path);
            }
        }

        return paths.OrderBy(static value => value, GetPathComparer()).ToArray();
    }

    /// <summary>把显式工作目录规范化到当前项目内，避免 Workbench 设置将模板写到未知位置。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <param name="lubanWorkDir">用户输入的工作目录。</param>
    /// <param name="workDirectory">成功时返回绝对工作目录。</param>
    /// <param name="diagnostic">失败时可显示的具体原因。</param>
    /// <returns>路径位于当前项目内且可规范化时返回 true。</returns>
    private static bool TryResolveProjectWorkDirectory(
        string projectRoot,
        string lubanWorkDir,
        out string workDirectory,
        out string diagnostic)
    {
        try
        {
            string fullDirectory = Path.GetFullPath(Path.IsPathFullyQualified(lubanWorkDir)
                ? lubanWorkDir
                : Path.Combine(projectRoot, lubanWorkDir));
            string relativePath = Path.GetRelativePath(projectRoot, fullDirectory);
            bool outsideProject = relativePath.Equals("..", StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathFullyQualified(relativePath);
            if (outsideProject)
            {
                workDirectory = string.Empty;
                diagnostic = "Luban 工作目录必须位于当前项目内。";
                return false;
            }

            workDirectory = fullDirectory;
            diagnostic = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            workDirectory = string.Empty;
            diagnostic = "Luban 工作目录无效: " + exception.Message;
            return false;
        }
    }

    /// <summary>寻找配置目录附近和项目标准 Luban 目录下的唯一可执行文件。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <param name="configDirectory">已解析的 luban.conf 目录。</param>
    /// <returns>按路径排序且去重的工具文件列表。</returns>
    private static IReadOnlyList<string> FindExecutablePaths(string projectRoot, string configDirectory)
    {
        HashSet<string> paths = new(GetPathComparer());
        AddExecutableCandidates(paths, configDirectory);
        AddExecutableCandidates(paths, Path.Combine(configDirectory, "..", "Tools", "Luban"));
        AddExecutableCandidates(paths, Path.Combine(projectRoot, "Luban", "Tools", "Luban"));
        AddExecutableCandidates(paths, Path.Combine(projectRoot, "Tools", "Luban"));
        return paths.OrderBy(static value => value, GetPathComparer()).ToArray();
    }

    /// <summary>尽力发现新版 Luban 的可选伴随工具；候选有歧义时返回空文本而不阻断主工具。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <param name="mainExecutablePath">已选中的主 Luban 入口。</param>
    /// <param name="directoryName">伴随工具目录名。</param>
    /// <param name="fileName">伴随工具基名，不含扩展名。</param>
    /// <returns>唯一可确认的 DLL/EXE 路径；缺失或歧义时返回空文本。</returns>
    private static string FindOptionalExecutablePath(
        string projectRoot,
        string configDirectory,
        string mainExecutablePath,
        string directoryName,
        string fileName)
    {
        HashSet<string> candidates = new(GetPathComparer());
        foreach (string directory in GetCompanionDirectories(projectRoot, configDirectory, mainExecutablePath, directoryName))
        {
            AddOptionalExecutableCandidate(candidates, directory, fileName);
        }

        return candidates.Count == 1 ? candidates.Single() : string.Empty;
    }

    /// <summary>发现项目内常见 Luban 文档目录；文档属于可选 AI 辅助能力。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <param name="mainExecutablePath">已选中的主 Luban 入口。</param>
    /// <returns>唯一可确认的文档目录；缺失或歧义时返回空文本。</returns>
    private static string FindOptionalDocumentationPath(
        string projectRoot,
        string configDirectory,
        string mainExecutablePath)
    {
        HashSet<string> candidates = new(GetPathComparer());
        AddExistingDirectory(candidates, Path.Combine(projectRoot, "luban-doc", "docs"));
        AddExistingDirectory(candidates, Path.Combine(projectRoot, "Luban", "luban-doc", "docs"));
        AddExistingDirectory(candidates, Path.Combine(configDirectory, "..", "..", "luban-doc", "docs"));
        AddExistingDirectory(candidates, Path.Combine(Path.GetDirectoryName(mainExecutablePath)!, "..", "..", "..", "luban-doc", "docs"));
        return candidates.Count == 1 ? candidates.Single() : string.Empty;
    }

    /// <summary>发现新版 Luban 官方 Skills 源目录；安装到 AI 客户端的目标目录不在此处自动写入。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <param name="mainExecutablePath">已选中的主 Luban 入口。</param>
    /// <returns>唯一可确认且包含官方 SKILL.md 的 Skills 根目录；缺失或歧义时返回空文本。</returns>
    private static string FindOptionalSkillsPath(
        string projectRoot,
        string configDirectory,
        string mainExecutablePath)
    {
        HashSet<string> candidates = new(GetPathComparer());
        AddExistingSkillsRoot(candidates, Path.Combine(projectRoot, "Luban", "Tools", "Luban.Skill", "skills"));
        AddExistingSkillsRoot(candidates, Path.Combine(projectRoot, "Luban", "Tools", "Luban.Skill"));
        AddExistingSkillsRoot(candidates, Path.Combine(projectRoot, "ai", "skills"));
        AddExistingSkillsRoot(candidates, Path.Combine(projectRoot, "Luban", "ai", "skills"));
        AddExistingSkillsRoot(candidates, Path.Combine(configDirectory, "..", "..", "ai", "skills"));
        AddExistingSkillsRoot(candidates, Path.Combine(Path.GetDirectoryName(mainExecutablePath)!, "..", "..", "..", "ai", "skills"));
        return candidates.Count == 1 ? candidates.Single() : string.Empty;
    }

    /// <summary>生成伴随工具的候选目录，并按当前平台路径规则去重。</summary>
    /// <param name="projectRoot">已规范化的项目根。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <param name="mainExecutablePath">已选中的主 Luban 入口。</param>
    /// <param name="directoryName">伴随工具目录名。</param>
    /// <returns>按优先级排列的候选目录。</returns>
    private static IReadOnlyList<string> GetCompanionDirectories(
        string projectRoot,
        string configDirectory,
        string mainExecutablePath,
        string directoryName)
    {
        string mainDirectory = Path.GetDirectoryName(mainExecutablePath)!;
        HashSet<string> directories = new(GetPathComparer());
        directories.Add(Path.GetFullPath(Path.Combine(mainDirectory, "..", directoryName)));
        directories.Add(Path.GetFullPath(Path.Combine(configDirectory, "..", "Tools", directoryName)));
        directories.Add(Path.GetFullPath(Path.Combine(projectRoot, "Luban", "Tools", directoryName)));
        directories.Add(Path.GetFullPath(Path.Combine(projectRoot, "Tools", directoryName)));
        return directories.OrderBy(static value => value, GetPathComparer()).ToArray();
    }

    /// <summary>把目录中的可选伴随工具按 DLL 优先加入候选集合。</summary>
    /// <param name="paths">去重候选集合。</param>
    /// <param name="directory">待检查目录。</param>
    /// <param name="fileName">工具基名，不含扩展名。</param>
    private static void AddOptionalExecutableCandidate(HashSet<string> paths, string directory, string fileName)
    {
        string assemblyPath = Path.Combine(directory, fileName + ".dll");
        if (File.Exists(assemblyPath))
        {
            paths.Add(Path.GetFullPath(assemblyPath));
            return;
        }

        AddExistingFile(paths, Path.Combine(directory, fileName + ".exe"));
    }

    /// <summary>在目录存在时按绝对路径加入可选文档目录候选。</summary>
    /// <param name="paths">去重候选集合。</param>
    /// <param name="path">待检查目录。</param>
    private static void AddExistingDirectory(HashSet<string> paths, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            paths.Add(fullPath);
        }
    }

    /// <summary>仅接受实际包含官方 Skill 定义的目录，避免把空目录误报为可用能力。</summary>
    /// <param name="paths">待写入的候选集合。</param>
    /// <param name="path">待检查的 Skills 根目录。</param>
    private static void AddExistingSkillsRoot(HashSet<string> paths, string path)
    {
        string? skillsRoot = ResolveOfficialSkillsRoot(path);
        if (!string.IsNullOrWhiteSpace(skillsRoot)) paths.Add(skillsRoot);
    }

    /// <summary>兼容用户选择 Luban.Skill 根目录或其中的 skills 子目录。</summary>
    /// <param name="path">待检查的目录。</param>
    /// <returns>包含官方 SKILL.md 的 skills 根目录；无法确认时返回空文本。</returns>
    public static string ResolveOfficialSkillsRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath)) return string.Empty;
        if (ContainsOfficialSkill(fullPath)) return fullPath;
        string nested = Path.Combine(fullPath, "skills");
        return ContainsOfficialSkill(nested) ? nested : string.Empty;
    }

    /// <summary>判断目录是否直接包含至少一个官方 Skill 定义。</summary>
    private static bool ContainsOfficialSkill(string path)
    {
        return Directory.Exists(path)
            && Directory.EnumerateDirectories(path).Any(static directory => File.Exists(Path.Combine(directory, "SKILL.md")));
    }

    /// <summary>根据常见 client/all 约定选择 target；其余多 target 配置要求调用方显式配置。</summary>
    /// <param name="targetNames">luban.conf 中声明的 target 名称。</param>
    /// <returns>可安全自动使用的 target，歧义时返回空文本。</returns>
    private static string SelectTarget(IReadOnlyList<string> targetNames)
    {
        if (targetNames.Count == 1)
        {
            return targetNames[0];
        }

        string? client = targetNames.FirstOrDefault(static target => string.Equals(target, "client", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(client))
        {
            return client;
        }

        return string.Empty;
    }

    /// <summary>把目录中的首选 Luban 入口加入候选集合；同目录 DLL 与 EXE 视为同一份工具安装。</summary>
    /// <param name="paths">去重集合。</param>
    /// <param name="directory">待检查目录。</param>
    private static void AddExecutableCandidates(HashSet<string> paths, string directory)
    {
        string fullDirectory = Path.GetFullPath(directory);
        string assemblyPath = Path.Combine(fullDirectory, "Luban.dll");
        if (File.Exists(assemblyPath))
        {
            // 官方 Windows 包通常同时提供 DLL 与 EXE，统一优先 DLL 以保持 TableKit 的既有调用契约。
            paths.Add(assemblyPath);
            return;
        }

        AddExistingFile(paths, Path.Combine(fullDirectory, "Luban.exe"));
    }

    /// <summary>在文件存在时按绝对路径加入候选集合。</summary>
    /// <param name="paths">去重集合。</param>
    /// <param name="path">待验证文件路径。</param>
    private static void AddExistingFile(HashSet<string> paths, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            paths.Add(fullPath);
        }
    }

    /// <summary>取得与当前平台路径语义一致的去重和排序比较器。</summary>
    /// <returns>Windows 忽略大小写，POSIX 保留大小写的路径比较器。</returns>
    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>创建统一的发现失败结果，避免调用方依赖异常字符串。</summary>
    /// <param name="diagnostic">面向用户的具体失败原因。</param>
    /// <returns>没有工具参数的失败结果。</returns>
    private static LubanToolDiscoveryResult Failed(string diagnostic, LubanConfiguration? configuration = null) => new()
    {
        Succeeded = false,
        Configuration = configuration,
        Diagnostics = new[] { diagnostic }
    };
}
