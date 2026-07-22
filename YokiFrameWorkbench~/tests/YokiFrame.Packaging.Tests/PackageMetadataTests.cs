using System.Text.Json;
using System.Text.RegularExpressions;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 约束 Unity Git URL 包根的身份与公开元数据，避免发布清单继续描述旧技术栈或未迁移能力。
/// </summary>
public sealed class PackageMetadataTests
{
    private const string PACKAGE_NAME = "com.hinatayoki.yokiframe";
    private const string UNITY_VERSION = "2022.3";
    private const string REPOSITORY_URL = "https://github.com/HinataYoki/YokiFrame.git";
    private const string VERSION_PATTERN = @"^2\.0\.(?:0|[1-9][0-9]*)-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*$";

    private static readonly HashSet<string> sRequiredCapabilityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "avalonia",
        "filebridge",
        "godot",
        "unity"
    };

    private static readonly HashSet<string> sMigratedKitKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "eventkit",
        "logkit",
        "poolkit",
        "singletonkit"
    };

    private static readonly HashSet<string> sForbiddenKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "buffkit",
        "inputkit",
        "tauri"
    };

    /// <summary>
    /// 验证包名、2.0 预发布 SemVer 与 Unity 最低版本保持发布契约要求。
    /// </summary>
    [Fact]
    public void PackageIdentityTargetsExpectedUnityPrerelease()
    {
        using var manifest = ReadPackageManifest();
        var root = manifest.RootElement;

        Assert.Equal(PACKAGE_NAME, root.GetProperty("name").GetString());
        Assert.Equal(UNITY_VERSION, root.GetProperty("unity").GetString());

        var version = root.GetProperty("version").GetString();
        Assert.NotNull(version);
        Assert.Matches(new Regex(VERSION_PATTERN, RegexOptions.CultureInvariant), version);
    }

    /// <summary>
    /// 验证 repository 指向 YokiFrame 仓库根 Git URL，确保 Unity 可直接通过 Git URL 安装包根。
    /// </summary>
    [Fact]
    public void PackageRepositoryPointsToRootGitUrl()
    {
        using var manifest = ReadPackageManifest();
        var repository = manifest.RootElement.GetProperty("repository");

        Assert.Equal("git", repository.GetProperty("type").GetString());
        Assert.Equal(REPOSITORY_URL, repository.GetProperty("url").GetString());
    }

    /// <summary>
    /// 验证包描述不再把已移除的 Tauri 工作台声明为当前产品能力。
    /// </summary>
    [Fact]
    public void PackageDescriptionDoesNotAdvertiseTauri()
    {
        using var manifest = ReadPackageManifest();
        var description = manifest.RootElement.GetProperty("description").GetString();

        Assert.NotNull(description);
        Assert.DoesNotContain("tauri", description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证关键词只公开已落地能力，包含当前宿主和通信方案，并排除旧技术栈、废弃 Kit 与未迁移 Kit。
    /// </summary>
    [Fact]
    public void PackageKeywordsMatchCurrentDeliveredCapabilities()
    {
        using var manifest = ReadPackageManifest();
        var keywords = ReadKeywords(manifest.RootElement);
        var violations = new List<string>();

        var missingKeywords = sRequiredCapabilityKeywords.Where(keyword => !keywords.Contains(keyword)).Order().ToArray();
        if (missingKeywords.Length > 0)
        {
            violations.Add("缺少当前能力关键词: " + string.Join(", ", missingKeywords));
        }

        var forbiddenKeywords = keywords.Where(sForbiddenKeywords.Contains).Order().ToArray();
        if (forbiddenKeywords.Length > 0)
        {
            violations.Add("仍包含旧技术栈或废弃 Kit: " + string.Join(", ", forbiddenKeywords));
        }

        var unmigratedKitKeywords = keywords
            .Where(IsUnmigratedKitKeyword)
            .Order()
            .ToArray();
        if (unmigratedKitKeywords.Length > 0)
        {
            violations.Add("仍包含未迁移 Kit: " + string.Join(", ", unmigratedKitKeywords));
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// 读取 package.json，并保持 JsonDocument 生命周期由测试方法显式释放。
    /// </summary>
    /// <returns>已解析的包清单文档。</returns>
    private static JsonDocument ReadPackageManifest()
    {
        var packagePath = Path.Combine(FindPackageRoot(), "package.json");
        Assert.True(File.Exists(packagePath), "缺少 Unity 包清单: " + packagePath);
        return JsonDocument.Parse(File.ReadAllText(packagePath));
    }

    /// <summary>
    /// 将关键词数组读取为不区分大小写的集合，便于稳定比较公开能力名称。
    /// </summary>
    /// <param name="root">package.json 根对象。</param>
    /// <returns>清单中的非空关键词集合。</returns>
    private static HashSet<string> ReadKeywords(JsonElement root)
    {
        return root.GetProperty("keywords")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断关键词是否把尚未完成迁移的具体 Kit 暴露为已交付能力；通用 kit 分类词不参与判断。
    /// </summary>
    /// <param name="keyword">待检查的关键词。</param>
    /// <returns>关键词以 kit 结尾且不在已迁移清单中时返回 true。</returns>
    private static bool IsUnmigratedKitKeyword(string keyword)
    {
        return !string.Equals(keyword, "kit", StringComparison.OrdinalIgnoreCase)
               && keyword.EndsWith("kit", StringComparison.OrdinalIgnoreCase)
               && !sMigratedKitKeywords.Contains(keyword);
    }

    /// <summary>
    /// 从 Unity 工程或独立包测试进程目录向上定位包含 package.json 的 YokiFrame 包根。
    /// </summary>
    /// <returns>YokiFrame 包根绝对路径。</returns>
    private static string FindPackageRoot()
    {
        var packageRoot = FindPackageRootFrom(Directory.GetCurrentDirectory())
            ?? FindPackageRootFrom(AppContext.BaseDirectory);
        return packageRoot ?? throw new DirectoryNotFoundException("无法定位包含 package.json 的 YokiFrame 包根。");
    }

    /// <summary>
    /// 从指定目录向上兼容查找独立包根或 Unity 工程内的 YokiFrame 包根。
    /// </summary>
    /// <param name="startPath">向上查找的起始目录。</param>
    /// <returns>找到的包根；未找到时返回 null。</returns>
    private static string? FindPackageRootFrom(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            var directManifest = Path.Combine(current.FullName, "package.json");
            if (current.Name == "YokiFrame" && File.Exists(directManifest))
            {
                return current.FullName;
            }

            var unityPackageRoot = Path.Combine(current.FullName, "Assets", "YokiFrame");
            if (File.Exists(Path.Combine(unityPackageRoot, "package.json")))
            {
                return unityPackageRoot;
            }

            current = current.Parent;
        }

        return null;
    }
}
