using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 识别 Unity 或 Godot 目标项目，并计算 YokiFrame 包目标根。
/// </summary>
public sealed class TargetProjectDetector
{
    private const int MINIMUM_UNITY_MAJOR = 2022;
    private const int MINIMUM_UNITY_MINOR = 3;
    private const string UNITY_PACKAGE_NAME = "com.hinatayoki.yokiframe";
    private const string GODOT_SDK_PREFIX = "Godot.NET.Sdk/";
    private const int MINIMUM_GODOT_MAJOR = 4;
    private const int MINIMUM_GODOT_MINOR = 7;
    private const int MINIMUM_DOTNET_MAJOR = 8;

    private static readonly Regex sUnityVersionRegex = new(
        @"^(?<major>[0-9]+)\.(?<minor>[0-9]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex sTargetFrameworkRegex = new(
        @"^net(?<major>[0-9]+)\.(?<minor>[0-9]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 检测目标项目类型。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>目标项目信息。</returns>
    public InstallerProjectInfo Detect(string projectRoot)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        if (!Directory.Exists(fullProjectRoot))
        {
            throw new DirectoryNotFoundException("Target project root was not found: " + fullProjectRoot);
        }

        if (IsUnityProject(fullProjectRoot))
        {
            return CreateUnityProjectInfo(fullProjectRoot);
        }

        if (IsGodotProject(fullProjectRoot))
        {
            return CreateGodotProjectInfo(fullProjectRoot);
        }

        return new InstallerProjectInfo(
            InstallerProjectKind.Unknown,
            fullProjectRoot,
            string.Empty,
            Array.Empty<string>());
    }

    /// <summary>
    /// 判断目录是否符合 Unity 项目最小结构。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>符合时返回 true。</returns>
    private static bool IsUnityProject(string projectRoot)
    {
        return Directory.Exists(InstallerPathGuard.CombineInside(projectRoot, "Assets"))
            && File.Exists(InstallerPathGuard.CombineInside(projectRoot, "Packages", "manifest.json"))
            && Directory.Exists(InstallerPathGuard.CombineInside(projectRoot, "ProjectSettings"));
    }

    /// <summary>
    /// 判断目录是否符合 Godot 项目最小结构。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>符合时返回 true。</returns>
    private static bool IsGodotProject(string projectRoot)
    {
        return File.Exists(InstallerPathGuard.CombineInside(projectRoot, "project.godot"))
            && FindGodotCSharpProject(projectRoot) != null;
    }

    /// <summary>
    /// 创建 Unity 项目信息。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>目标项目信息。</returns>
    private static InstallerProjectInfo CreateUnityProjectInfo(string projectRoot)
    {
        ValidateUnityVersion(projectRoot);
        return new InstallerProjectInfo(
            InstallerProjectKind.Unity,
            projectRoot,
            InstallerPathGuard.CombineInside(projectRoot, "Packages", UNITY_PACKAGE_NAME),
            new[]
            {
                InstallerPathGuard.CombineInside(projectRoot, "Assets"),
                InstallerPathGuard.CombineInside(projectRoot, "Packages", "manifest.json"),
                InstallerPathGuard.CombineInside(projectRoot, "ProjectSettings")
            });
    }

    /// <summary>
    /// 创建 Godot 项目信息。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>目标项目信息。</returns>
    private static InstallerProjectInfo CreateGodotProjectInfo(string projectRoot)
    {
        var csharpProjectPath = FindGodotCSharpProject(projectRoot)
            ?? throw new InvalidDataException("Godot .NET project is missing a top-level C# project file.");
        ValidateGodotProject(csharpProjectPath);
        List<string> evidencePaths = new()
        {
            InstallerPathGuard.CombineInside(projectRoot, "project.godot")
        };
        var godotCachePath = InstallerPathGuard.CombineInside(projectRoot, ".godot");
        if (Directory.Exists(godotCachePath))
        {
            evidencePaths.Add(godotCachePath);
        }

        evidencePaths.Add(csharpProjectPath);

        return new InstallerProjectInfo(
            InstallerProjectKind.Godot,
            projectRoot,
            InstallerPathGuard.CombineInside(projectRoot, "addons", "yokiframe", "package", "YokiFrame"),
            evidencePaths);
    }

    /// <summary>
    /// 查找 Godot .NET 项目的顶层 C# 项目文件。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>C# 项目文件路径；不存在时返回 null。</returns>
    private static string? FindGodotCSharpProject(string projectRoot)
    {
        return Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// 读取 Unity ProjectVersion.txt 并验证最低支持版本为 2022.3.x。
    /// </summary>
    /// <param name="projectRoot">Unity 项目根目录。</param>
    private static void ValidateUnityVersion(string projectRoot)
    {
        var versionPath = InstallerPathGuard.CombineInside(projectRoot, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(versionPath))
        {
            throw new InvalidDataException("Unity project is missing ProjectSettings/ProjectVersion.txt; YokiFrame requires Unity 2022.3 or newer.");
        }

        var version = ReadUnityEditorVersion(versionPath);
        var match = sUnityVersionRegex.Match(version);
        if (!match.Success
            || !int.TryParse(match.Groups["major"].Value, out var major)
            || !int.TryParse(match.Groups["minor"].Value, out var minor))
        {
            throw new InvalidDataException("Unity editor version is invalid: " + version);
        }

        if (major < MINIMUM_UNITY_MAJOR || (major == MINIMUM_UNITY_MAJOR && minor < MINIMUM_UNITY_MINOR))
        {
            throw new InvalidDataException("YokiFrame requires Unity 2022.3 or newer; detected " + version + ".");
        }
    }

    /// <summary>
    /// 从 Unity 版本文件中提取 m_EditorVersion 值。
    /// </summary>
    /// <param name="versionPath">ProjectVersion.txt 绝对路径。</param>
    /// <returns>Unity Editor 版本文本。</returns>
    private static string ReadUnityEditorVersion(string versionPath)
    {
        const string prefix = "m_EditorVersion:";
        foreach (var line in File.ReadLines(versionPath))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        throw new InvalidDataException("Unity ProjectVersion.txt does not contain m_EditorVersion; YokiFrame requires Unity 2022.3 or newer.");
    }

    /// <summary>
    /// 解析 Godot C# 项目并验证 Godot.NET.Sdk 4.7+ 与 net8.0+ 下限，供项目识别和安装计划共享。
    /// </summary>
    /// <param name="projectPath">Godot 主 C# 项目路径。</param>
    internal static void ValidateGodotProject(string projectPath)
    {
        var project = LoadGodotProject(projectPath);
        var sdk = (string?)project.Root?.Attribute("Sdk") ?? string.Empty;
        var sdkVersion = sdk.StartsWith(GODOT_SDK_PREFIX, StringComparison.Ordinal)
            ? sdk[GODOT_SDK_PREFIX.Length..]
            : string.Empty;
        if (!Version.TryParse(sdkVersion, out var version)
            || version.Major < MINIMUM_GODOT_MAJOR
            || (version.Major == MINIMUM_GODOT_MAJOR && version.Minor < MINIMUM_GODOT_MINOR))
        {
            throw new InvalidDataException("YokiFrame requires Godot.NET.Sdk 4.7 or newer; detected " + (sdkVersion.Length == 0 ? sdk : sdkVersion) + ".");
        }

        var targetFramework = project.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "TargetFramework" && element.Attribute("Condition") == null)
            ?.Value.Trim();
        var frameworkMatch = sTargetFrameworkRegex.Match(targetFramework ?? string.Empty);
        if (!frameworkMatch.Success
            || !int.TryParse(frameworkMatch.Groups["major"].Value, out var frameworkMajor)
            || frameworkMajor < MINIMUM_DOTNET_MAJOR)
        {
            throw new InvalidDataException("YokiFrame Godot desktop projects require net8.0 or newer; detected " + (targetFramework ?? "missing") + ".");
        }
    }

    /// <summary>
    /// 加载 Godot 主项目 XML，并把语法或根节点错误转换为安装诊断。
    /// </summary>
    /// <param name="projectPath">Godot 主 C# 项目路径。</param>
    /// <returns>已验证 Project 根文档。</returns>
    private static XDocument LoadGodotProject(string projectPath)
    {
        try
        {
            var project = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            if (project.Root == null || project.Root.Name.LocalName != "Project")
            {
                throw new InvalidDataException("Godot C# project must use an MSBuild Project root: " + projectPath);
            }

            return project;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Godot C# project XML is invalid: " + projectPath, exception);
        }
    }
}
