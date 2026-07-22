using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using YokiFrame.Client;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 从少量权威项目文件读取 Project Model 输入，不扫描用户脚本或构建产物。
/// </summary>
internal sealed class ProjectModelSourceScanner
{
    private const string PACKAGE_NAME = "com.hinatayoki.yokiframe";
    private readonly IYokiFrameClient mClient;

    /// <summary>使用统一 Client 的项目路径和 raw harness 入口创建扫描器。</summary>
    public ProjectModelSourceScanner(IYokiFrameClient client)
    {
        mClient = client;
    }

    /// <summary>
    /// 读取项目类型、包身份、依赖和 package-owned descriptor。
    /// </summary>
    /// <param name="packageRootHint">可选包根提示；必须位于项目内部。</param>
    /// <returns>规范化的输入快照。</returns>
    public ProjectModelSourceSnapshot Scan(string packageRootHint)
    {
        _ = NormalizeInsideProject(mClient.Paths.ProjectRoot);
        var project = DetectProject();
        var packageRoot = ResolvePackageRoot(project, packageRootHint);
        var packageManifestPath = NormalizeInsideProject(Path.Combine(packageRoot, "package.json"));
        var package = ReadJsonObject(packageManifestPath, "PackageManifestInvalid");
        var packageName = ReadRequiredString(package, "name", packageManifestPath);
        if (!string.Equals(packageName, PACKAGE_NAME, StringComparison.Ordinal))
        {
            throw CreateError(
                "ProjectPackageIdentityMismatch",
                "The selected package is not the YokiFrame package: " + packageName,
                "Select a package whose package.json name is com.hinatayoki.yokiframe.",
                new[] { packageManifestPath });
        }
        var packageVersion = ReadRequiredString(package, "version", packageManifestPath);
        var packageRelativeRoot = ToProjectRelativePath(packageRoot);
        var descriptorPaths = FindCapabilityDescriptors(packageRoot);
        var dependencySourcePath = NormalizeInsideProject(ResolveDependencySource(project));
        var dependencies = ReadDependencies(project, dependencySourcePath);
        var sourcePaths = CreateSourcePaths(project, packageRoot, packageManifestPath, dependencySourcePath, descriptorPaths);
        return new ProjectModelSourceSnapshot(
            project.ProjectRoot,
            project.Kind.ToString(),
            ReadEngineVersion(project),
            packageRoot,
            packageRelativeRoot,
            packageName,
            packageVersion,
            ResolvePackageSource(packageRelativeRoot),
            sourcePaths.Select(CreateSourceFile).ToArray(),
            dependencies,
            descriptorPaths.Select(ToProjectRelativePath).ToArray(),
            new ProjectHarnessDeclarations(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
    }

    /// <summary>调用 Installer 的跨引擎检测器，并把未知项目转换为稳定错误。</summary>
    private InstallerProjectInfo DetectProject()
    {
        var project = new TargetProjectDetector().Detect(mClient.Paths.ProjectRoot);
        if (project.Kind == InstallerProjectKind.Unknown)
        {
            throw CreateError(
                "ProjectTypeUnsupported",
                "Project Model supports Unity 2022.3+ or Godot .NET projects.",
                "Select a supported project root and retry project refresh.",
                new[] { mClient.Paths.ProjectRoot });
        }

        return project;
    }

    /// <summary>按显式提示、bootstrap 和受支持安装布局顺序解析真实包根。</summary>
    private string ResolvePackageRoot(InstallerProjectInfo project, string packageRootHint)
    {
        foreach (var candidate in CreatePackageCandidates(project, packageRootHint))
        {
            var fullPath = NormalizeInsideProject(candidate);
            if (File.Exists(Path.Combine(fullPath, "package.json")))
            {
                return fullPath;
            }
        }

        throw CreateError(
            "ProjectPackageUnresolved",
            "YokiFrame package root could not be resolved inside the project.",
            "Wait for package resolution or pass --package with the installed package root.",
            new[] { mClient.Paths.ProjectRoot });
    }

    /// <summary>创建不会越过项目根的候选包路径集合。</summary>
    private IReadOnlyList<string> CreatePackageCandidates(InstallerProjectInfo project, string packageRootHint)
    {
        List<string> candidates = new();
        AddCandidate(candidates, packageRootHint);
        AddCandidate(candidates, ReadHarnessPackageRoot());
        AddCandidate(candidates, Path.Combine(project.ProjectRoot, "Assets", "YokiFrame"));
        AddCandidate(candidates, project.PackageRoot);
        AddCandidate(candidates, Path.Combine(project.ProjectRoot, "addons", "yokiframe", "package", "YokiFrame"));
        AddPackageCacheCandidates(project.ProjectRoot, candidates);
        return candidates;
    }

    /// <summary>向候选集合加入非空且尚未出现的路径。</summary>
    private void AddCandidate(ICollection<string> candidates, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var path = Path.IsPathRooted(candidate)
            ? candidate
            : Path.Combine(mClient.Paths.ProjectRoot, candidate.Replace('/', Path.DirectorySeparatorChar));
        if (!candidates.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(path);
        }
    }

    /// <summary>精确枚举 Unity PackageCache 中已解析的 YokiFrame Git 包候选。</summary>
    private static void AddPackageCacheCandidates(string projectRoot, ICollection<string> candidates)
    {
        var cacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");
        if (!Directory.Exists(cacheRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateDirectories(cacheRoot, PACKAGE_NAME + "@*", SearchOption.TopDirectoryOnly)
                     .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(path);
        }
    }

    /// <summary>从 raw bootstrap 中读取包根提示；缺失或损坏时由其它确定性布局继续解析。</summary>
    private string ReadHarnessPackageRoot()
    {
        try
        {
            return mClient.ReadHarnessCapabilities()["package"]?["packageRoot"] is JsonValue value
                && value.TryGetValue<string>(out var packageRoot)
                ? packageRoot
                : string.Empty;
        }
        catch (Exception exception) when (exception is YokiFrameProtocolException or JsonException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    /// <summary>解析项目引擎版本，不复用动态 registry 版本。</summary>
    private string ReadEngineVersion(InstallerProjectInfo project)
    {
        return project.Kind == InstallerProjectKind.Unity
            ? ReadUnityVersion(NormalizeInsideProject(Path.Combine(project.ProjectRoot, "ProjectSettings", "ProjectVersion.txt")))
            : ReadGodotVersion(NormalizeInsideProject(FindGodotProjectFile(project.ProjectRoot)));
    }

    /// <summary>从 Unity ProjectVersion.txt 读取 m_EditorVersion。</summary>
    private static string ReadUnityVersion(string path)
    {
        const string prefix = "m_EditorVersion:";
        var line = File.ReadLines(path).FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        return line == null ? string.Empty : line[prefix.Length..].Trim();
    }

    /// <summary>从 Godot csproj 的 SDK 属性读取 4.7 版本。</summary>
    private static string ReadGodotVersion(string projectPath)
    {
        var root = LoadXml(projectPath).Root;
        var sdk = (string?)root?.Attribute("Sdk") ?? string.Empty;
        const string prefix = "Godot.NET.Sdk/";
        return sdk.StartsWith(prefix, StringComparison.Ordinal) ? sdk[prefix.Length..] : sdk;
    }

    /// <summary>解析 Unity manifest 或 Godot csproj 的依赖列表。</summary>
    private IReadOnlyList<ProjectDependencyFact> ReadDependencies(InstallerProjectInfo project, string sourcePath)
    {
        return project.Kind == InstallerProjectKind.Unity
            ? ReadUnityDependencies(sourcePath)
            : ReadGodotDependencies(sourcePath);
    }

    /// <summary>结构化读取 Unity Packages/manifest.json dependencies。</summary>
    private IReadOnlyList<ProjectDependencyFact> ReadUnityDependencies(string manifestPath)
    {
        var root = ReadJsonObject(manifestPath, "ProjectDependenciesInvalid");
        var relativePath = ToProjectRelativePath(manifestPath);
        if (root["dependencies"] is not JsonObject dependencies)
        {
            throw CreateError("ProjectDependenciesInvalid", "Unity manifest dependencies must be an object.", "Repair Packages/manifest.json.", new[] { manifestPath });
        }

        return dependencies
            .Select(entry => new ProjectDependencyFact(
                entry.Key,
                entry.Value?.GetValue<string>() ?? string.Empty,
                "Package",
                relativePath))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>结构化读取 Godot csproj 的 SDK、PackageReference 和 ProjectReference。</summary>
    private IReadOnlyList<ProjectDependencyFact> ReadGodotDependencies(string projectPath)
    {
        var document = LoadXml(projectPath);
        var relativePath = ToProjectRelativePath(projectPath);
        List<ProjectDependencyFact> dependencies = new()
        {
            new ProjectDependencyFact("Godot.NET.Sdk", ReadGodotVersion(projectPath), "EngineSdk", relativePath)
        };
        foreach (var element in document.Descendants().Where(static element =>
                     element.Name.LocalName is "PackageReference" or "ProjectReference"))
        {
            var name = (string?)element.Attribute("Include") ?? string.Empty;
            var reference = (string?)element.Attribute("Version") ?? element.Value.Trim();
            dependencies.Add(new ProjectDependencyFact(name, reference, element.Name.LocalName == "PackageReference" ? "Package" : "Project", relativePath));
        }

        return dependencies.OrderBy(static item => item.Kind, StringComparer.Ordinal)
            .ThenBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>解析当前项目的依赖权威文件。</summary>
    private static string ResolveDependencySource(InstallerProjectInfo project)
    {
        return project.Kind == InstallerProjectKind.Unity
            ? Path.Combine(project.ProjectRoot, "Packages", "manifest.json")
            : FindGodotProjectFile(project.ProjectRoot);
    }

    /// <summary>查找唯一顶层 Godot C# 项目文件。</summary>
    private static string FindGodotProjectFile(string projectRoot)
    {
        return Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>只在 Core/Runtime 与 Tools 中查找 package-owned capability descriptor。</summary>
    private IReadOnlyList<string> FindCapabilityDescriptors(string packageRoot)
    {
        List<string> paths = new();
        AddDescriptorRoot(Path.Combine(packageRoot, "Core", "Runtime"), paths);
        AddDescriptorRoot(Path.Combine(packageRoot, "Tools"), paths);
        return paths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>从受控包目录递归加入 capability.json，不遍历 WorkbenchRuntime 或用户项目。</summary>
    private void AddDescriptorRoot(string root, ICollection<string> paths)
    {
        var fullRoot = NormalizeInsideProject(root);
        if (!Directory.Exists(fullRoot))
        {
            return;
        }

        Stack<string> pending = new();
        pending.Push(fullRoot);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var path in Directory.EnumerateFiles(current, "capability.json", SearchOption.TopDirectoryOnly))
            {
                paths.Add(NormalizeInsideProject(path));
            }

            foreach (var directory in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
            {
                pending.Push(NormalizeInsideProject(directory));
            }
        }
    }

    /// <summary>创建参与 input hash 的权威文件集合。</summary>
    private IReadOnlyList<string> CreateSourcePaths(
        InstallerProjectInfo project,
        string packageRoot,
        string packageManifestPath,
        string dependencySourcePath,
        IReadOnlyList<string> descriptorPaths)
    {
        List<string> paths = new() { packageManifestPath, dependencySourcePath };
        paths.Add(project.Kind == InstallerProjectKind.Unity
            ? Path.Combine(project.ProjectRoot, "ProjectSettings", "ProjectVersion.txt")
            : Path.Combine(project.ProjectRoot, "project.godot"));
        paths.AddRange(descriptorPaths);
        paths.AddRange(ReadDescriptorSourcePaths(packageRoot, descriptorPaths));
        var lockPath = Path.Combine(project.ProjectRoot, "Packages", "packages-lock.json");
        if (project.Kind == InstallerProjectKind.Unity && File.Exists(lockPath))
        {
            paths.Add(lockPath);
        }

        AddRuntimeCacheSourcePaths(paths, project.ProjectRoot, packageRoot);

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>把当前源码指纹对应的项目 Runtime 指针和 manifest 纳入输入 hash，不读取包内二进制目录。</summary>
    private static void AddRuntimeCacheSourcePaths(ICollection<string> paths, string projectRoot, string packageRoot)
    {
        var runtimeCache = new ProjectRuntimeCacheReader().Read(projectRoot, packageRoot);
        if (File.Exists(runtimeCache.PointerPath))
        {
            paths.Add(runtimeCache.PointerPath);
        }

        if (File.Exists(runtimeCache.ManifestPath))
        {
            paths.Add(runtimeCache.ManifestPath);
        }
    }

    /// <summary>读取 descriptor 指向的实现源码路径，使源码改动进入 Project Model input hash。</summary>
    private IReadOnlyList<string> ReadDescriptorSourcePaths(string packageRoot, IReadOnlyList<string> descriptorPaths)
    {
        List<string> paths = new();
        foreach (var descriptorPath in descriptorPaths)
        {
            var descriptor = ReadJsonObject(descriptorPath, "CapabilityDescriptorInvalid");
            var sourcePath = descriptor["kit"]?["sourcePath"] is JsonValue value
                && value.TryGetValue<string>(out var relativePath)
                ? relativePath
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                paths.Add(ResolveDescriptorSourcePath(packageRoot, sourcePath, descriptorPath));
            }
        }

        return paths;
    }

    /// <summary>把 descriptor 的实现来源限制为包内普通文件，并拒绝跨平台绝对路径与重解析路径链。</summary>
    private static string ResolveDescriptorSourcePath(string packageRoot, string sourcePath, string descriptorPath)
    {
        if (ProjectModelPathPolicy.IsPortableRooted(sourcePath))
        {
            throw CreateError(
                "CapabilitySourceInvalid",
                "Capability sourcePath must be a package-relative file path.",
                "Repair the package capability descriptor and retry.",
                new[] { descriptorPath });
        }

        var fullPath = Path.GetFullPath(Path.Combine(packageRoot, sourcePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!ProjectModelPathPolicy.IsInsideOrSame(packageRoot, fullPath))
        {
            throw CreateError(
                "CapabilitySourceEscapesPackage",
                "Capability sourcePath escapes the YokiFrame package.",
                "Keep capability implementation sources inside the selected package.",
                new[] { descriptorPath, fullPath });
        }

        if (ProjectModelPathPolicy.ContainsReparsePoint(packageRoot, fullPath))
        {
            throw CreateError(
                "CapabilitySourceReparsePoint",
                "Capability sourcePath contains a symbolic link or junction.",
                "Replace the linked path with a package-owned regular file.",
                new[] { descriptorPath, fullPath });
        }

        return fullPath;
    }

    /// <summary>读取源文件并计算确定性 SHA-256。</summary>
    private ProjectModelSourceFile CreateSourceFile(string path)
    {
        var fullPath = NormalizeInsideProject(path);
        if (!File.Exists(fullPath))
        {
            throw CreateError(
                "ProjectModelSourceMissing",
                "Project Model source file is missing: " + fullPath,
                "Restore the package or repair the project source before refreshing.",
                new[] { fullPath });
        }

        using var stream = File.OpenRead(fullPath);
        return new ProjectModelSourceFile(fullPath, ToProjectRelativePath(fullPath), Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
    }

    /// <summary>把绝对路径转换为不会逃逸的项目相对正斜杠路径。</summary>
    private string ToProjectRelativePath(string path)
    {
        var fullPath = NormalizeInsideProject(path);
        return Path.GetRelativePath(mClient.Paths.ProjectRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>规范化路径并拒绝项目根之外的 package 或 evidence。</summary>
    private string NormalizeInsideProject(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!ProjectModelPathPolicy.IsInsideOrSame(mClient.Paths.ProjectRoot, fullPath))
        {
            throw CreateError("ProjectPathEscapesRoot", "Project Model source path escapes the project root.", "Use an installed package path inside the target project.", new[] { fullPath });
        }

        if (ProjectModelPathPolicy.ContainsReparsePoint(mClient.Paths.ProjectRoot, fullPath))
        {
            throw CreateError(
                "ProjectPathReparsePoint",
                "Project Model source path contains a symbolic link or junction.",
                "Use ordinary project-local files for package and Project Model sources.",
                new[] { fullPath });
        }

        return fullPath;
    }

    /// <summary>根据项目相对包根确定安装来源语义。</summary>
    private static string ResolvePackageSource(string relativeRoot)
    {
        if (relativeRoot.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return "Development";
        if (relativeRoot.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)) return "Embedded";
        if (relativeRoot.StartsWith("Library/PackageCache/", StringComparison.OrdinalIgnoreCase)) return "GitCache";
        if (relativeRoot.StartsWith("addons/yokiframe/", StringComparison.OrdinalIgnoreCase)) return "GodotProjection";
        return "ProjectLocal";
    }

    /// <summary>读取 JSON 对象并把语法错误映射为稳定项目错误。</summary>
    private static JsonObject ReadJsonObject(string path, string errorCode)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw CreateError(errorCode, "JSON root must be an object: " + path, "Repair the source JSON and retry.", new[] { path });
        }
        catch (JsonException exception)
        {
            throw CreateError(errorCode, "JSON is invalid: " + exception.Message, "Repair the source JSON and retry.", new[] { path });
        }
    }

    /// <summary>读取 JSON 必填字符串。</summary>
    private static string ReadRequiredString(JsonObject root, string propertyName, string path)
    {
        var value = root[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var parsed)
            ? parsed
            : string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CreateError("PackageManifestInvalid", "package.json is missing " + propertyName + ".", "Repair the package manifest and retry.", new[] { path });
        }

        return value;
    }

    /// <summary>加载 XML 并把语法错误转换为稳定项目错误。</summary>
    private static XDocument LoadXml(string path)
    {
        try
        {
            return XDocument.Load(path, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw CreateError("ProjectDependenciesInvalid", "Project XML is invalid: " + exception.Message, "Repair the project file and retry.", new[] { path });
        }
    }

    /// <summary>创建携带恢复建议和证据路径的协议异常。</summary>
    private static YokiFrameProtocolException CreateError(
        string code,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        return new YokiFrameProtocolException(new YokiFrameError(code, message, suggestion, evidencePaths));
    }
}
