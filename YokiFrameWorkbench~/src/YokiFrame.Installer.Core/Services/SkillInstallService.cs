using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 提供包内 AI Skill 扫描、安装和卸载能力。
/// </summary>
public sealed class SkillInstallService
{
    private const string PACKAGE_NAME = "com.hinatayoki.yokiframe";
    private const string SKILL_FILE_NAME = "SKILL.md";
    private const string SKILL_ROOT = "Core/Editor/Skills";
    private static readonly IReadOnlyList<SkillTarget> sKnownTargets = new[]
    {
        new SkillTarget("claude", "Claude Code", ".claude/skills"),
        new SkillTarget("codex", "Codex", ".codex/skills"),
        new SkillTarget("cursor", "Cursor", ".cursor/skills"),
        new SkillTarget("windsurf", "Windsurf", ".windsurf/skills"),
        new SkillTarget("github-copilot", "GitHub Copilot", ".github/skills"),
        new SkillTarget("agents", "Agents", ".agents/skills"),
        new SkillTarget("custom", "Custom", ".custom/skills")
    };

    /// <summary>
    /// 扫描当前项目中 YokiFrame 包内 Skill 和各目标安装状态。
    /// </summary>
    /// <param name="projectRoot">Unity、Godot 或包根所在项目根目录。</param>
    /// <returns>Skill 安装状态。</returns>
    public SkillInstallStatus GetStatus(string projectRoot)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var sourceRoot = ResolveSkillSourceRoot(fullProjectRoot);
        var skills = CollectPackagedSkills(sourceRoot);
        var targets = sKnownTargets
            .Select(target => CreateTargetStatus(fullProjectRoot, target))
            .ToArray();
        return new SkillInstallStatus(ToSlash(fullProjectRoot), ToSlash(sourceRoot), skills, targets);
    }

    /// <summary>
    /// 把指定包内 Skill 安装到目标 AI 助手目录。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="customPath">自定义目标根目录；仅 targetId 为 custom 时使用。</param>
    /// <returns>安装结果。</returns>
    public SkillInstallResult Install(string projectRoot, string targetId, string skillName, string? customPath = null)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var sourceDir = ResolveSkillSourceDirectory(fullProjectRoot, skillName);
        var targetDir = ResolveSkillTargetDirectory(fullProjectRoot, targetId, skillName, customPath);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }

        CopySkillDirectory(sourceDir, targetDir);
        return new SkillInstallResult(
            true,
            true,
            skillName,
            targetId,
            ToSlash(targetDir),
            "已安装 " + skillName + " 到 " + ToSlash(targetDir));
    }

    /// <summary>
    /// 从目标 AI 助手目录卸载指定 Skill。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="customPath">自定义目标根目录；仅 targetId 为 custom 时使用。</param>
    /// <returns>卸载结果。</returns>
    public SkillInstallResult Uninstall(string projectRoot, string targetId, string skillName, string? customPath = null)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var targetDir = ResolveSkillTargetDirectory(fullProjectRoot, targetId, skillName, customPath);
        var existed = Directory.Exists(targetDir);
        if (existed)
        {
            Directory.Delete(targetDir, recursive: true);
        }

        var log = existed ? "已卸载 " + skillName : skillName + " 尚未安装";
        return new SkillInstallResult(true, false, skillName, targetId, ToSlash(targetDir), log);
    }

    /// <summary>
    /// 创建单个安装目标的状态。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="target">目标定义。</param>
    /// <returns>目标状态。</returns>
    private static SkillInstallTargetStatus CreateTargetStatus(string projectRoot, SkillTarget target)
    {
        var customPath = target.Id == "custom" ? target.RelativePath : null;
        var targetRoot = ResolveSkillTargetRoot(projectRoot, target.Id, target.RelativePath, customPath);
        var installedSkills = CollectInstalledSkills(targetRoot);
        return new SkillInstallTargetStatus(
            target.Id,
            target.Label,
            target.RelativePath,
            target.Id == "custom",
            installedSkills);
    }

    /// <summary>
    /// 收集包内存在 SKILL.md 的 Skill 目录。
    /// </summary>
    /// <param name="sourceRoot">Skill 源根目录。</param>
    /// <returns>Skill 列表。</returns>
    private static IReadOnlyList<SkillInstallInfo> CollectPackagedSkills(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return Array.Empty<SkillInstallInfo>();
        }

        return Directory.EnumerateDirectories(sourceRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && IsSafeSkillName(name))
            .Select(name => name!)
            .Where(name => File.Exists(Path.Combine(sourceRoot, name, SKILL_FILE_NAME)))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(name => new SkillInstallInfo(name, true, ToSlash(Path.Combine(sourceRoot, name))))
            .ToArray();
    }

    /// <summary>
    /// 收集指定 AI 目标目录下已安装的 Skill。
    /// </summary>
    /// <param name="targetRoot">AI 目标根目录。</param>
    /// <returns>已安装 Skill 名称。</returns>
    private static IReadOnlyList<string> CollectInstalledSkills(string targetRoot)
    {
        if (!Directory.Exists(targetRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateDirectories(targetRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && IsSafeSkillName(name))
            .Select(name => name!)
            .Where(name => File.Exists(Path.Combine(targetRoot, name, SKILL_FILE_NAME)))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 解析指定 Skill 的包内源目录。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <returns>Skill 源目录。</returns>
    private static string ResolveSkillSourceDirectory(string projectRoot, string skillName)
    {
        RequireSafeSkillName(skillName);
        foreach (var sourceRoot in CreateSkillSourceRootCandidates(projectRoot))
        {
            var sourceDir = Path.Combine(sourceRoot, skillName);
            if (File.Exists(Path.Combine(sourceDir, SKILL_FILE_NAME)))
            {
                return sourceDir;
            }
        }

        throw new DirectoryNotFoundException("YokiFrame 包内 Skill 不存在: " + skillName);
    }

    /// <summary>
    /// 解析项目中当前可用的 Skill 源根目录。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <returns>Skill 源根目录。</returns>
    private static string ResolveSkillSourceRoot(string projectRoot)
    {
        var candidates = CreateSkillSourceRootCandidates(projectRoot);
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    /// <summary>
    /// 创建所有支持的 Skill 源根候选，覆盖嵌入包、本地包、UPM cache 和 Godot 插件布局。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <returns>候选目录列表。</returns>
    private static IReadOnlyList<string> CreateSkillSourceRootCandidates(string projectRoot)
    {
        List<string> candidates = new()
        {
            Path.Combine(projectRoot, SKILL_ROOT),
            Path.Combine(projectRoot, "Assets", "YokiFrame", SKILL_ROOT),
            Path.Combine(projectRoot, "Packages", PACKAGE_NAME, SKILL_ROOT),
            Path.Combine(projectRoot, "addons", "yokiframe", "package", "YokiFrame", SKILL_ROOT)
        };
        AddPackageCacheCandidates(candidates, projectRoot);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// 追加 Unity PackageCache 中的 YokiFrame Skill 源候选。
    /// </summary>
    /// <param name="candidates">候选目录列表。</param>
    /// <param name="projectRoot">项目根目录。</param>
    private static void AddPackageCacheCandidates(List<string> candidates, string projectRoot)
    {
        var packageCacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");
        if (!Directory.Exists(packageCacheRoot))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(packageCacheRoot, PACKAGE_NAME + "@*").OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(directory, SKILL_ROOT));
        }
    }

    /// <summary>
    /// 解析某个 Skill 的最终安装目录，并确保不会写出项目根。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="customPath">自定义目标根目录。</param>
    /// <returns>Skill 目标目录。</returns>
    private static string ResolveSkillTargetDirectory(string projectRoot, string targetId, string skillName, string? customPath)
    {
        RequireSafeSkillName(skillName);
        var target = FindTarget(targetId);
        var relativeRoot = target.Id == "custom" ? customPath : target.RelativePath;
        var targetRoot = ResolveSkillTargetRoot(projectRoot, target.Id, relativeRoot, customPath);
        return InstallerPathGuard.CombineInside(targetRoot, skillName);
    }

    /// <summary>
    /// 解析目标根目录，并拒绝绝对路径、盘符和相对逃逸。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="relativeRoot">默认相对目录。</param>
    /// <param name="customPath">用户自定义目录。</param>
    /// <returns>目标根目录。</returns>
    private static string ResolveSkillTargetRoot(string projectRoot, string targetId, string? relativeRoot, string? customPath)
    {
        var value = targetId == "custom" ? customPath : relativeRoot;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Skill 安装路径不能为空。");
        }

        var normalized = value.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Skill 安装路径必须位于项目根目录内: " + value);
        }

        try
        {
            return InstallerPathGuard.CombineInside(projectRoot, normalized);
        }
        catch (IOException exception)
        {
            throw new ArgumentException("Skill 安装路径必须位于项目根目录内: " + value, exception);
        }
    }

    /// <summary>
    /// 复制 Skill 目录到目标目录；Unity meta 与占位文件不进入 AI 目录。
    /// </summary>
    /// <param name="sourceDir">源目录。</param>
    /// <param name="targetDir">目标目录。</param>
    private static void CopySkillDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(sourceDir))
        {
            var name = Path.GetFileName(sourcePath);
            if (ShouldSkipSkillEntry(sourcePath, name))
            {
                continue;
            }

            var targetPath = Path.Combine(targetDir, name);
            if (Directory.Exists(sourcePath))
            {
                CopySkillDirectory(sourcePath, targetPath);
            }
            else if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
            }
        }
    }

    /// <summary>
    /// 判断目录项是否应从 AI Skill 安装结果中排除。
    /// </summary>
    /// <param name="sourcePath">源路径。</param>
    /// <param name="name">文件或目录名。</param>
    /// <returns>需要跳过时返回 true。</returns>
    private static bool ShouldSkipSkillEntry(string sourcePath, string name)
    {
        var attributes = File.GetAttributes(sourcePath);
        return attributes.HasFlag(FileAttributes.ReparsePoint)
            || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ".keep", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 查找已知安装目标。
    /// </summary>
    /// <param name="targetId">目标标识。</param>
    /// <returns>目标定义。</returns>
    private static SkillTarget FindTarget(string targetId)
    {
        return sKnownTargets.FirstOrDefault(target => target.Id == targetId)
            ?? throw new ArgumentException("未知 AI Skill 安装目标: " + targetId);
    }

    /// <summary>
    /// 校验 Skill 名称，避免路径穿越和隐藏文件名。
    /// </summary>
    /// <param name="skillName">Skill 名称。</param>
    private static void RequireSafeSkillName(string skillName)
    {
        if (!IsSafeSkillName(skillName))
        {
            throw new ArgumentException("无效 Skill 名称: " + skillName, nameof(skillName));
        }
    }

    /// <summary>
    /// 判断 Skill 名称是否只包含小写字母、数字和连字符。
    /// </summary>
    /// <param name="value">Skill 名称。</param>
    /// <returns>安全时返回 true。</returns>
    private static bool IsSafeSkillName(string value)
    {
        return value.Length is > 0 and <= 128
            && value != "."
            && value != ".."
            && value.All(static item => char.IsAsciiLetterLower(item) || char.IsAsciiDigit(item) || item == '-');
    }

    /// <summary>
    /// 把路径转换为 UI 和日志中稳定的斜杠格式。
    /// </summary>
    /// <param name="path">本机路径。</param>
    /// <returns>斜杠路径。</returns>
    private static string ToSlash(string path)
    {
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// 描述内置 AI Skill 安装目标。
    /// </summary>
    /// <param name="Id">目标标识。</param>
    /// <param name="Label">显示名。</param>
    /// <param name="RelativePath">相对项目根的安装目录。</param>
    private sealed record SkillTarget(string Id, string Label, string RelativePath);
}
