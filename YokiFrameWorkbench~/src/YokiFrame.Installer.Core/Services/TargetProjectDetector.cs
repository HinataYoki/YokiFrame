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
    private const string GODOT_DOTNET_SECTION = "[dotnet]";

    private static readonly Regex sUnityVersionRegex = new(
        @"^(?<major>[0-9]+)\.(?<minor>[0-9]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex sTargetFrameworkRegex = new(
        @"^net(?<major>[0-9]+)\.(?<minor>[0-9]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex sGodotFeatureVersionRegex = new(
        @"config/features\s*=\s*PackedStringArray\(\s*""(?<major>[0-9]+)\.(?<minor>[0-9]+)",
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
        var projectSettingsPath = InstallerPathGuard.CombineInside(projectRoot, "project.godot");
        return File.Exists(projectSettingsPath)
            && (FindGodotCSharpProject(projectRoot) != null
                || HasGodotDotNetEvidence(projectRoot, projectSettingsPath));
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
        var csharpProjectPath = FindGodotCSharpProject(projectRoot);
        if (csharpProjectPath != null)
        {
            ValidateGodotProject(csharpProjectPath);
        }

        List<string> evidencePaths = new()
        {
            InstallerPathGuard.CombineInside(projectRoot, "project.godot")
        };
        var godotCachePath = InstallerPathGuard.CombineInside(projectRoot, ".godot");
        if (Directory.Exists(godotCachePath))
        {
            evidencePaths.Add(godotCachePath);
        }

        if (csharpProjectPath != null)
        {
            evidencePaths.Add(csharpProjectPath);
        }

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
    /// 判断 Godot 项目是否留下 .NET 编辑器或配置证据，兼容 Godot 尚未生成主 csproj 的新项目。
    /// </summary>
    /// <param name="projectRoot">目标 Godot 项目根目录。</param>
    /// <param name="projectSettingsPath">project.godot 绝对路径。</param>
    /// <returns>发现 .NET 证据时返回 true。</returns>
    internal static bool HasGodotDotNetEvidence(string projectRoot, string projectSettingsPath)
    {
        var monoDirectory = InstallerPathGuard.CombineInside(projectRoot, ".godot", "mono");
        if (Directory.Exists(monoDirectory))
        {
            return true;
        }

        // Godot project.godot 不是标准 JSON/XML；这里只解析 section header，避免依赖宿主编辑器或脆弱的全文匹配。
        foreach (var rawLine in File.ReadLines(projectSettingsPath))
        {
            var line = rawLine.Trim();
            if (string.Equals(line, GODOT_DOTNET_SECTION, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 在空项目生成主 csproj 前校验 project.godot 已记录的 Godot 主版本下限。
    /// </summary>
    /// <param name="projectSettingsPath">project.godot 绝对路径。</param>
    internal static void ValidateGodotProjectFeatureVersion(string projectSettingsPath)
    {
        foreach (var rawLine in File.ReadLines(projectSettingsPath))
        {
            var match = sGodotFeatureVersionRegex.Match(rawLine);
            if (!match.Success
                || !int.TryParse(match.Groups["major"].Value, out var major)
                || !int.TryParse(match.Groups["minor"].Value, out var minor))
            {
                continue;
            }

            if (major < MINIMUM_GODOT_MAJOR
                || (major == MINIMUM_GODOT_MAJOR && minor < MINIMUM_GODOT_MINOR))
            {
                throw new InvalidDataException(
                    "YokiFrame requires Godot 4.7 or newer; detected " + major + "." + minor + ".");
            }

            return;
        }
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
        ValidateGodotProjectDocument(project, projectPath);
    }

    /// <summary>
    /// 验证尚未落盘的 Godot 主项目 XML，供空 Godot .NET 项目生成主项目文件前复用同一门控。
    /// </summary>
    /// <param name="projectContent">待验证的完整 MSBuild XML。</param>
    /// <param name="projectPath">计划写入的主项目路径，仅用于错误定位。</param>
    internal static void ValidateGodotProjectContent(string projectContent, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectContent);
        try
        {
            var project = XDocument.Parse(projectContent, LoadOptions.PreserveWhitespace);
            ValidateGodotProjectDocument(project, projectPath);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Godot C# project XML is invalid: " + projectPath, exception);
        }
    }

    /// <summary>
    /// 验证 Godot 主项目 XML 的 SDK 与桌面目标框架下限。
    /// </summary>
    /// <param name="project">已解析的 MSBuild Project 文档。</param>
    /// <param name="projectPath">主项目路径，仅用于错误定位。</param>
    private static void ValidateGodotProjectDocument(XDocument project, string projectPath)
    {
        if (project.Root == null || project.Root.Name.LocalName != "Project")
        {
            throw new InvalidDataException("Godot C# project must use an MSBuild Project root: " + projectPath);
        }

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
            return project;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Godot C# project XML is invalid: " + projectPath, exception);
        }
    }
}
