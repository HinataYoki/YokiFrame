using System.Text.RegularExpressions;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 扫描用户 C# 代码对旧 Kit API 的真实标识符引用，并根据当前源包文件判断 Kit 是否已经提供。
/// </summary>
internal sealed class ProjectKitReferenceScanner
{
    private static readonly Regex NonCodePattern = new(
        "/\\*.*?\\*/|//[^\\r\\n]*|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex IdentifierPattern = new(
        "[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex YokiFrameReferencePattern = new(
        "\\busing\\s+(?:(?:static\\s+)?YokiFrame\\b|[A-Za-z_][A-Za-z0-9_]*\\s*=\\s*YokiFrame\\s*;)|\\bYokiFrame\\s*\\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly KitDescriptor[] LegacyKits =
    {
        new("ActionKit", new[] { "ActionKit", "ActionStackTraceService" }),
        new("AudioKit", new[] { "AudioKit" }),
        new("BuffKit", new[] { "BuffKit" }),
        new("FsmKit", new[] { "FsmKit", "FsmKitCommandHandler", "FSM" }),
        new("InputKit", new[] { "InputKit" }),
        new("LocalizationKit", new[] { "LocalizationKit" }),
        new("ManagedRuntimeKit", new[] { "ManagedRuntimeKit" }),
        new("ResKit", new[] { "ResKit", "ResHandle" }),
        new("SaveKit", new[] { "SaveKit" }),
        new("SceneKit", new[] { "SceneKit" }),
        new("SpatialKit", new[] { "SpatialKit", "SpatialHashGrid" }),
        new("UIKit", new[] { "UIKit", "UIPanel", "UILevel", "UIRoot", "IUIData", "IPanel" })
    };

    /// <summary>
    /// 扫描项目中由用户拥有的 C# 文件，返回当前源包无法满足的旧 Kit 引用。
    /// </summary>
    /// <param name="projectRoot">已规范化的 Godot 项目根。</param>
    /// <param name="sourcePackageRoot">已规范化的 YokiFrame 源包根。</param>
    /// <returns>按脚本、行号和 Kit 稳定排序的冲突。</returns>
    internal IReadOnlyList<KitReferenceConflict> Scan(string projectRoot, string sourcePackageRoot)
    {
        var unavailableIdentifiers = BuildUnavailableIdentifierMap(sourcePackageRoot);
        if (unavailableIdentifiers.Count == 0)
        {
            return Array.Empty<KitReferenceConflict>();
        }

        List<KitReferenceConflict> conflicts = new();
        foreach (var filePath in EnumerateUserCodeFiles(projectRoot))
        {
            ScanFile(projectRoot, filePath, unavailableIdentifiers, conflicts);
        }

        return conflicts
            .OrderBy(static conflict => conflict.ProjectRelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static conflict => conflict.LineNumber)
            .ThenBy(static conflict => conflict.KitName, StringComparer.Ordinal)
            .ThenBy(static conflict => conflict.Identifier, StringComparer.Ordinal)
            .DistinctBy(static conflict => (
                conflict.ProjectRelativePath,
                conflict.KitName))
            .ToArray();
    }

    /// <summary>
    /// 根据源包内是否存在对应 Kit 的 C# 实现，建立旧 API 标识符到缺失 Kit 的映射。
    /// </summary>
    /// <param name="sourcePackageRoot">YokiFrame 源包根。</param>
    /// <returns>仅包含当前不可用 Kit 的标识符映射。</returns>
    private static IReadOnlyDictionary<string, string> BuildUnavailableIdentifierMap(string sourcePackageRoot)
    {
        Dictionary<string, string> identifiers = new(StringComparer.Ordinal);
        foreach (var kit in LegacyKits)
        {
            if (HasCurrentImplementation(sourcePackageRoot, kit.Name))
            {
                continue;
            }

            foreach (var identifier in kit.Identifiers)
            {
                identifiers[identifier] = kit.Name;
            }
        }

        return identifiers;
    }

    /// <summary>
    /// 判断当前源包的 Core 或 Tools Kit 目录是否已经包含可投影 C# 实现。
    /// </summary>
    /// <param name="sourcePackageRoot">YokiFrame 源包根。</param>
    /// <param name="kitName">Kit 名称。</param>
    /// <returns>至少存在一个 C# 实现文件时返回 true。</returns>
    private static bool HasCurrentImplementation(string sourcePackageRoot, string kitName)
    {
        var coreRoot = Path.Combine(sourcePackageRoot, "Core", "Runtime", kitName);
        var toolRoot = Path.Combine(sourcePackageRoot, "Tools", kitName);
        return HasCSharpFile(coreRoot) || HasCSharpFile(toolRoot);
    }

    /// <summary>
    /// 判断目录是否包含至少一个可进入 Godot 发布投影的 C# 文件；不存在的目录直接视为未实现。
    /// </summary>
    /// <param name="directory">待检查目录。</param>
    /// <returns>存在 C# 文件时返回 true。</returns>
    private static bool HasCSharpFile(string directory)
    {
        return Directory.Exists(directory)
            && Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Any(path => IsGodotProjectableCSharpFile(directory, path));
    }

    /// <summary>
    /// 判断候选源码是否属于 Godot 可用的 Runtime 实现，排除 Unity 专属、Editor、测试和工具目录。
    /// </summary>
    /// <param name="implementationRoot">当前 Kit 的实现扫描根目录。</param>
    /// <param name="filePath">候选 C# 文件绝对路径。</param>
    /// <returns>文件能够作为 Godot Runtime 实现进入发布投影时返回 true。</returns>
    private static bool IsGodotProjectableCSharpFile(string implementationRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(implementationRoot, filePath);
        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (IsExcludedImplementationDirectory(segments, index))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 判断路径片段是否属于不会成为 Godot Runtime Kit 实现的目录边界。
    /// </summary>
    /// <param name="segments">候选文件相对 Kit 根的路径片段。</param>
    /// <param name="index">当前待检查目录片段索引。</param>
    /// <returns>当前片段应从实现检测中排除时返回 true。</returns>
    private static bool IsExcludedImplementationDirectory(IReadOnlyList<string> segments, int index)
    {
        var segment = segments[index];
        if (string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "Tests", StringComparison.OrdinalIgnoreCase)
            || segment.EndsWith('~'))
        {
            return true;
        }

        return index > 0
            && string.Equals(segment, "Unity", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(segments[index - 1], "Adapters", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[index - 1], "Integrations", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 枚举用户拥有的 C# 文件，排除 Installer 管理目录和 Godot/.NET 生成缓存。
    /// </summary>
    /// <param name="projectRoot">Godot 项目根。</param>
    /// <returns>稳定排序的用户脚本绝对路径。</returns>
    private static IEnumerable<string> EnumerateUserCodeFiles(string projectRoot)
    {
        return Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsExcludedProjectPath(projectRoot, path))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断脚本是否位于 Installer 管理目录、版本控制目录或构建缓存中。
    /// </summary>
    /// <param name="projectRoot">Godot 项目根。</param>
    /// <param name="filePath">候选脚本绝对路径。</param>
    /// <returns>不属于用户编译源码面时返回 true。</returns>
    private static bool IsExcludedProjectPath(string projectRoot, string filePath)
    {
        var relativePath = "/" + Path.GetRelativePath(projectRoot, filePath).Replace('\\', '/') + "/";
        return relativePath.StartsWith("/addons/yokiframe/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("/.godot/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("/.git/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("/.yokiframe/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 扫描单个用户脚本；只有代码面明确引用 YokiFrame 时才解释旧 Kit 标识符。
    /// </summary>
    /// <param name="projectRoot">Godot 项目根。</param>
    /// <param name="filePath">用户脚本绝对路径。</param>
    /// <param name="unavailableIdentifiers">旧标识符到缺失 Kit 的映射。</param>
    /// <param name="conflicts">冲突收集列表。</param>
    private static void ScanFile(
        string projectRoot,
        string filePath,
        IReadOnlyDictionary<string, string> unavailableIdentifiers,
        List<KitReferenceConflict> conflicts)
    {
        var maskedSource = NonCodePattern.Replace(File.ReadAllText(filePath), MaskNonCode);
        if (!YokiFrameReferencePattern.IsMatch(maskedSource))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(projectRoot, filePath).Replace('\\', '/');
        var lines = maskedSource.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            AddLineConflicts(relativePath, index + 1, lines[index], unavailableIdentifiers, conflicts);
        }
    }

    /// <summary>
    /// 把一行代码中命中的旧 API 标识符转换为带文件和行号的冲突。
    /// </summary>
    /// <param name="relativePath">项目内脚本路径。</param>
    /// <param name="lineNumber">一基行号。</param>
    /// <param name="line">已经屏蔽注释和字面量的代码行。</param>
    /// <param name="unavailableIdentifiers">旧标识符映射。</param>
    /// <param name="conflicts">冲突收集列表。</param>
    private static void AddLineConflicts(
        string relativePath,
        int lineNumber,
        string line,
        IReadOnlyDictionary<string, string> unavailableIdentifiers,
        List<KitReferenceConflict> conflicts)
    {
        foreach (Match match in IdentifierPattern.Matches(line))
        {
            if (unavailableIdentifiers.TryGetValue(match.Value, out var kitName))
            {
                conflicts.Add(new KitReferenceConflict(kitName, match.Value, relativePath, lineNumber));
            }
        }
    }

    /// <summary>
    /// 用空格替换注释和字面量，同时保留换行以维持真实脚本行号。
    /// </summary>
    /// <param name="match">正则命中的非代码片段。</param>
    /// <returns>长度和换行位置不变的屏蔽文本。</returns>
    private static string MaskNonCode(Match match)
    {
        var characters = match.Value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] != '\r' && characters[index] != '\n')
            {
                characters[index] = ' ';
            }
        }

        return new string(characters);
    }

    /// <summary>
    /// 描述旧 Kit 名称及其在 1.x/2.0-pre 用户代码中常见的公共标识符。
    /// </summary>
    /// <param name="Name">Kit 名称。</param>
    /// <param name="Identifiers">可识别的旧 API 标识符。</param>
    private sealed record KitDescriptor(string Name, IReadOnlyList<string> Identifiers);
}
