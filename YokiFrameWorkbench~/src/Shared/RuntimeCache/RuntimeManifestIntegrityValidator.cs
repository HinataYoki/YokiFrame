using System.Text.Json;

namespace YokiFrame.RuntimeCache;

/// <summary>
/// 验证项目 Runtime 缓存 manifest 的版本、目标平台与入口，供 Packaging 与 Installer 共用。
/// </summary>
internal static class RuntimeManifestIntegrityValidator
{
    private const int MANIFEST_VERSION = 1;
    private const int LEGACY_LAYOUT_VERSION = 1;
    private const int DUAL_ENTRY_LAYOUT_VERSION = 2;
    private const long MAX_MANIFEST_BYTES = 16L * 1024L * 1024L;

    /// <summary>
    /// 验证指定平台记录、入口和完整文件清单；任何损坏、缺失或额外载荷都视为缓存不可用。
    /// </summary>
    /// <param name="manifestPath">Runtime manifest 完整路径。</param>
    /// <param name="runtimeRoot">源码指纹对应的 Runtime 根目录。</param>
    /// <param name="runtimeProfile">待验证平台 profile。</param>
    /// <param name="requireCli">是否要求 CLI 入口存在。</param>
    /// <param name="profile">验证成功后返回可信入口。</param>
    /// <param name="error">验证失败原因。</param>
    /// <returns>manifest 与磁盘载荷完全一致时返回 true。</returns>
    internal static bool TryValidateProfile(
        string manifestPath,
        string runtimeRoot,
        string runtimeProfile,
        bool requireCli,
        out RuntimeManifestProfileValidation profile,
        out string error)
    {
        profile = RuntimeManifestProfileValidation.Empty;
        error = string.Empty;
        try
        {
            using var document = ReadDocument(manifestPath);
            return TryValidateDocument(
                document.RootElement,
                Path.GetFullPath(runtimeRoot),
                runtimeProfile,
                requireCli,
                out profile,
                out error);
        }
        catch (Exception exception) when (IsRecoverableValidationFailure(exception))
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 在受限大小内读取 manifest，避免损坏缓存触发无界内存分配。
    /// </summary>
    /// <param name="manifestPath">manifest 完整路径。</param>
    /// <returns>已解析 JSON 文档。</returns>
    private static JsonDocument ReadDocument(string manifestPath)
    {
        var info = new FileInfo(manifestPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Runtime manifest was not found.", manifestPath);
        }

        if (info.Length <= 0 || info.Length > MAX_MANIFEST_BYTES)
        {
            throw new InvalidDataException("Runtime manifest size is invalid.");
        }

        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 64 });
    }

    /// <summary>
    /// 验证 manifest 头并定位唯一目标平台记录。
    /// </summary>
    /// <param name="root">manifest 根元素。</param>
    /// <param name="runtimeRoot">规范化 Runtime 根。</param>
    /// <param name="runtimeProfile">目标 profile。</param>
    /// <param name="requireCli">是否要求 CLI。</param>
    /// <param name="profile">可信入口结果。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>完整验证成功时返回 true。</returns>
    private static bool TryValidateDocument(
        JsonElement root,
        string runtimeRoot,
        string runtimeProfile,
        bool requireCli,
        out RuntimeManifestProfileValidation profile,
        out string error)
    {
        profile = RuntimeManifestProfileValidation.Empty;
        if (!TryValidateHeader(root, out var layoutVersion, out error)
            || !TryFindPlatform(root, runtimeProfile, out var platform, out error))
        {
            return false;
        }

        return TryValidatePlatform(
            platform,
            runtimeRoot,
            runtimeProfile,
            layoutVersion,
            requireCli,
            out profile,
            out error);
    }

    /// <summary>
    /// 校验当前支持的 manifest 与布局版本以及可搬运 Runtime 根标记。
    /// </summary>
    /// <param name="root">manifest 根元素。</param>
    /// <param name="layoutVersion">解析出的布局版本。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>头部结构有效时返回 true。</returns>
    private static bool TryValidateHeader(JsonElement root, out int layoutVersion, out string error)
    {
        layoutVersion = 0;
        error = string.Empty;
        if (root.ValueKind != JsonValueKind.Object
            || !RuntimeManifestJson.TryReadInt32(root, "manifestVersion", out var manifestVersion)
            || manifestVersion != MANIFEST_VERSION
            || !RuntimeManifestJson.TryReadInt32(root, "layoutVersion", out layoutVersion)
            || layoutVersion is not (LEGACY_LAYOUT_VERSION or DUAL_ENTRY_LAYOUT_VERSION)
            || !RuntimeManifestJson.TryReadString(root, "runtimeRoot", out var runtimeRoot)
            || !string.Equals(runtimeRoot, ".", StringComparison.Ordinal))
        {
            error = "Runtime manifest header is invalid.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 从平台数组中定位唯一的目标 profile，拒绝缺失或重复记录。
    /// </summary>
    /// <param name="root">manifest 根元素。</param>
    /// <param name="runtimeProfile">目标 profile。</param>
    /// <param name="platform">唯一平台记录。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>恰好存在一个目标平台时返回 true。</returns>
    private static bool TryFindPlatform(
        JsonElement root,
        string runtimeProfile,
        out JsonElement platform,
        out string error)
    {
        platform = default;
        error = string.Empty;
        if (!root.TryGetProperty("platforms", out var platforms) || platforms.ValueKind != JsonValueKind.Array)
        {
            error = "Runtime manifest does not contain a platforms array.";
            return false;
        }

        var matchCount = 0;
        foreach (var candidate in platforms.EnumerateArray())
        {
            if (RuntimeManifestJson.TryReadString(candidate, "platform", out var name)
                && string.Equals(name, runtimeProfile, StringComparison.Ordinal))
            {
                platform = candidate;
                matchCount++;
            }
        }

        if (matchCount != 1)
        {
            error = "Runtime manifest must contain exactly one target profile.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证单个平台的标识、文件清单、物理文件集合和入口。
    /// </summary>
    /// <param name="platform">目标平台 JSON。</param>
    /// <param name="runtimeRoot">Runtime 根。</param>
    /// <param name="runtimeProfile">目标 profile。</param>
    /// <param name="layoutVersion">manifest 布局版本。</param>
    /// <param name="requireCli">是否要求 CLI。</param>
    /// <param name="profile">可信入口结果。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>平台缓存完整时返回 true。</returns>
    private static bool TryValidatePlatform(
        JsonElement platform,
        string runtimeRoot,
        string runtimeProfile,
        int layoutVersion,
        bool requireCli,
        out RuntimeManifestProfileValidation profile,
        out string error)
    {
        profile = RuntimeManifestProfileValidation.Empty;
        if (!RuntimeManifestJson.TryReadString(platform, "runtimeIdentifier", out var runtimeIdentifier)
            || !string.Equals(runtimeIdentifier, runtimeProfile, StringComparison.Ordinal)
            || !RuntimeManifestPathPolicy.TryResolveDirectoryInside(runtimeRoot, runtimeProfile, out var platformRoot))
        {
            error = "Runtime manifest profile identifier or directory is invalid.";
            return false;
        }

        if (!RuntimeManifestFileSetValidator.TryValidate(
                platform,
                runtimeRoot,
                platformRoot,
                out var files,
                out error))
        {
            return false;
        }

        return TryValidateEntries(platform, runtimeRoot, files, layoutVersion, requireCli, out profile, out error);
    }

    /// <summary>
    /// 验证 GUI/CLI 入口位于 Runtime 根内、存在于已校验文件集合且符合布局版本。
    /// </summary>
    /// <param name="platform">目标平台 JSON。</param>
    /// <param name="runtimeRoot">Runtime 根。</param>
    /// <param name="files">可信文件集合。</param>
    /// <param name="layoutVersion">manifest 布局版本。</param>
    /// <param name="requireCli">是否要求 CLI。</param>
    /// <param name="profile">可信入口结果。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>入口均属于文件清单时返回 true。</returns>
    private static bool TryValidateEntries(
        JsonElement platform,
        string runtimeRoot,
        IReadOnlySet<string> files,
        int layoutVersion,
        bool requireCli,
        out RuntimeManifestProfileValidation profile,
        out string error)
    {
        profile = RuntimeManifestProfileValidation.Empty;
        var guiEntry = RuntimeManifestJson.ReadOptionalString(platform, "guiEntry");
        if (string.IsNullOrWhiteSpace(guiEntry))
        {
            guiEntry = RuntimeManifestJson.ReadOptionalString(platform, "entrypoint");
        }

        var cliEntry = RuntimeManifestJson.ReadOptionalString(platform, "cliEntry");
        if (!TryResolveListedEntry(runtimeRoot, guiEntry, files, out var guiPath)
            || requireCli && string.IsNullOrWhiteSpace(cliEntry)
            || !string.IsNullOrWhiteSpace(cliEntry) && layoutVersion != DUAL_ENTRY_LAYOUT_VERSION
            || !string.IsNullOrWhiteSpace(cliEntry)
                && !TryResolveListedEntry(runtimeRoot, cliEntry, files, out _))
        {
            error = "Runtime manifest GUI or CLI entry is invalid.";
            return false;
        }

        _ = TryResolveListedEntry(runtimeRoot, cliEntry, files, out var cliPath);
        profile = new RuntimeManifestProfileValidation(guiPath, cliPath);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 将入口解析到 Runtime 根，并要求它已经通过逐文件完整性校验。
    /// </summary>
    /// <param name="runtimeRoot">Runtime 根。</param>
    /// <param name="entry">manifest 相对入口。</param>
    /// <param name="files">可信文件集合。</param>
    /// <param name="fullPath">可信入口完整路径。</param>
    /// <returns>入口属于文件集合时返回 true。</returns>
    private static bool TryResolveListedEntry(
        string runtimeRoot,
        string entry,
        IReadOnlySet<string> files,
        out string fullPath)
    {
        return RuntimeManifestPathPolicy.TryResolveFileInside(runtimeRoot, entry, out fullPath)
            && files.Contains(fullPath);
    }

    /// <summary>
    /// 判断异常是否代表缓存内容不可用，而非调用方编程错误。
    /// </summary>
    /// <param name="exception">校验期间异常。</param>
    /// <returns>应转换为验证失败时返回 true。</returns>
    private static bool IsRecoverableValidationFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or ArgumentException
            or System.Security.Cryptography.CryptographicException
            or OverflowException;
    }
}

/// <summary>
/// 提供 manifest JSON 基础字段的严格类型读取，避免各验证阶段重复弱类型分支。
/// </summary>
internal static class RuntimeManifestJson
{
    /// <summary>
    /// 读取必需字符串属性。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">解析值。</param>
    /// <returns>属性存在且为非空字符串时返回 true。</returns>
    internal static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = ReadOptionalString(element, propertyName);
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// 读取可选字符串属性，缺失或类型错误时返回空文本。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <returns>字符串值或空文本。</returns>
    internal static string ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    /// <summary>
    /// 读取 32 位整数属性。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">解析值。</param>
    /// <returns>属性为有效整数时返回 true。</returns>
    internal static bool TryReadInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out value);
    }

    /// <summary>
    /// 读取 64 位整数属性。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">解析值。</param>
    /// <returns>属性为有效整数时返回 true。</returns>
    internal static bool TryReadInt64(JsonElement element, string propertyName, out long value)
    {
        value = 0L;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out value);
    }
}

/// <summary>
/// 保存经过 manifest 文件集合、长度和哈希验证的 Runtime 平台入口。
/// </summary>
/// <param name="GuiPath">可信 GUI 入口完整路径。</param>
/// <param name="CliPath">可信 CLI 入口完整路径；未发布时为空。</param>
internal sealed record RuntimeManifestProfileValidation(string GuiPath, string CliPath)
{
    /// <summary>获取验证失败时使用的空结果。</summary>
    internal static RuntimeManifestProfileValidation Empty { get; } = new(string.Empty, string.Empty);
}
