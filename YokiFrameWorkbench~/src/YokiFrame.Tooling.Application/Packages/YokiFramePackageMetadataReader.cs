using System.Text.Json;

namespace YokiFrame.Tooling.Application.Packages;

/// <summary>
/// 从明确的 YokiFrame 包根读取并验证 package.json 用户可见元数据。
/// </summary>
public static class YokiFramePackageMetadataReader
{
    private const string PACKAGE_FILE_NAME = "package.json";
    private const string GIT_SUFFIX = ".git";

    /// <summary>
    /// 读取包版本与仓库主页；缺失或无效字段会产生包含文件路径的诊断异常。
    /// </summary>
    /// <param name="packageRoot">启动入口解析出的真实 YokiFrame 包根。</param>
    /// <returns>经过验证的包元数据。</returns>
    public static YokiFramePackageMetadata Read(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        var packagePath = Path.Combine(Path.GetFullPath(packageRoot), PACKAGE_FILE_NAME);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("YokiFrame package.json 不存在。", packagePath);
        }

        try
        {
            using var stream = File.OpenRead(packagePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("YokiFrame package.json 根必须是对象: " + packagePath);
            }

            return new YokiFramePackageMetadata(
                ReadRequiredString(root, "version", packagePath),
                ReadRepositoryUri(root, packagePath));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YokiFrame package.json JSON 无效: " + packagePath, exception);
        }
    }

    /// <summary>
    /// 读取非空字符串字段，统一生成可定位到 package.json 的错误信息。
    /// </summary>
    /// <param name="root">package.json 根对象。</param>
    /// <param name="propertyName">必需字段名称。</param>
    /// <param name="packagePath">诊断使用的 package.json 绝对路径。</param>
    /// <returns>去除首尾空白后的字段值。</returns>
    private static string ReadRequiredString(
        JsonElement root,
        string propertyName,
        string packagePath)
    {
        if (!root.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new InvalidDataException(
                "YokiFrame package.json 缺少非空字符串 " + propertyName + ": " + packagePath);
        }

        return element.GetString()!.Trim();
    }

    /// <summary>
    /// 读取 repository 字符串或 repository.url 对象字段，并转换为浏览器主页。
    /// </summary>
    /// <param name="root">package.json 根对象。</param>
    /// <param name="packagePath">诊断使用的 package.json 绝对路径。</param>
    /// <returns>HTTPS 仓库主页。</returns>
    private static Uri ReadRepositoryUri(JsonElement root, string packagePath)
    {
        if (!root.TryGetProperty("repository", out var repository))
        {
            throw new InvalidDataException("YokiFrame package.json 缺少 repository: " + packagePath);
        }

        var repositoryUrl = repository.ValueKind switch
        {
            JsonValueKind.String => repository.GetString(),
            JsonValueKind.Object when repository.TryGetProperty("url", out var urlElement)
                && urlElement.ValueKind == JsonValueKind.String => urlElement.GetString(),
            _ => null
        };
        return CreateRepositoryHomepage(repositoryUrl, packagePath);
    }

    /// <summary>
    /// 移除 Git 克隆专用前后缀，并拒绝不能安全交给浏览器的地址。
    /// </summary>
    /// <param name="repositoryUrl">package.json 中的原始仓库地址。</param>
    /// <param name="packagePath">诊断使用的 package.json 绝对路径。</param>
    /// <returns>规范化后的 HTTPS 仓库主页。</returns>
    private static Uri CreateRepositoryHomepage(string? repositoryUrl, string packagePath)
    {
        var normalizedUrl = repositoryUrl?.Trim() ?? string.Empty;
        if (normalizedUrl.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = normalizedUrl[4..];
        }

        if (normalizedUrl.EndsWith(GIT_SUFFIX, StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = normalizedUrl[..^GIT_SUFFIX.Length];
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var repositoryUri)
            || !string.Equals(repositoryUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "YokiFrame package.json repository 必须是有效 HTTPS 地址: " + packagePath);
        }

        return repositoryUri;
    }
}
