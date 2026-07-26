using System.Security.Cryptography;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 为 WorkbenchRuntime 运行副本生成 manifest。
/// </summary>
public sealed class RuntimeManifestBuilder
{
    private const int MANIFEST_VERSION = 1;
    private const int LEGACY_LAYOUT_VERSION = 1;
    private const int DUAL_ENTRY_LAYOUT_VERSION = 2;
    private const string PORTABLE_RUNTIME_ROOT = ".";
    private const string RUNTIME_STATE_DIRECTORY_NAME = ".yokiframe";

    /// <summary>
    /// 根据 runtime root 和平台目录生成运行副本 manifest。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="product">产品名。</param>
    /// <param name="platform">平台标识。</param>
    /// <param name="entrypoint">平台 GUI 入口文件名。</param>
    /// <returns>运行副本 manifest。</returns>
    public RuntimeManifest Build(string runtimeRoot, string product, string platform, string entrypoint)
    {
        return BuildInternal(runtimeRoot, product, platform, entrypoint, string.Empty, sharedRuntime: false);
    }

    /// <summary>
    /// 根据 runtime root 和平台目录生成 GUI + CLI 共用运行时 manifest。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="product">产品名。</param>
    /// <param name="platform">平台标识。</param>
    /// <param name="guiEntry">Workbench GUI 入口文件名。</param>
    /// <param name="cliEntry">轻量 CLI 入口文件名。</param>
    /// <returns>运行副本 manifest。</returns>
    public RuntimeManifest Build(string runtimeRoot, string product, string platform, string guiEntry, string cliEntry)
    {
        return Build(
            runtimeRoot,
            product,
            platform,
            guiEntry,
            cliEntry,
            sharedRuntime: !string.IsNullOrWhiteSpace(cliEntry));
    }

    /// <summary>
    /// 根据 runtime root 和平台目录生成 GUI + CLI manifest，并显式声明两个入口是否共用运行时文件。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="product">产品名。</param>
    /// <param name="platform">平台标识。</param>
    /// <param name="guiEntry">Workbench GUI 入口文件名。</param>
    /// <param name="cliEntry">CLI 入口文件名；未发布 CLI 时为空。</param>
    /// <param name="sharedRuntime">GUI 与 CLI 是否共用运行时文件。</param>
    /// <returns>运行副本 manifest。</returns>
    public RuntimeManifest Build(
        string runtimeRoot,
        string product,
        string platform,
        string guiEntry,
        string cliEntry,
        bool sharedRuntime)
    {
        return BuildInternal(runtimeRoot, product, platform, guiEntry, cliEntry, sharedRuntime);
    }

    /// <summary>
    /// 基于已有 manifest 更新指定平台记录；用于跨平台逐个发布时保留其它平台产物信息。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="product">产品名。</param>
    /// <param name="existingManifest">已有 manifest；为空时等同于创建新 manifest。</param>
    /// <param name="platform">平台标识。</param>
    /// <param name="guiEntry">Workbench GUI 入口文件名。</param>
    /// <param name="cliEntry">轻量 CLI 入口文件名。</param>
    /// <returns>合并后的运行副本 manifest。</returns>
    public RuntimeManifest Build(
        string runtimeRoot,
        string product,
        RuntimeManifest? existingManifest,
        string platform,
        string guiEntry,
        string cliEntry)
    {
        return Build(
            runtimeRoot,
            product,
            existingManifest,
            platform,
            guiEntry,
            cliEntry,
            sharedRuntime: !string.IsNullOrWhiteSpace(cliEntry));
    }

    /// <summary>
    /// 基于已有 manifest 更新指定平台记录，并显式保留 GUI 与 CLI 的运行时共享关系。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="product">产品名。</param>
    /// <param name="existingManifest">已有 manifest；为空时等同于创建新 manifest。</param>
    /// <param name="platform">平台标识。</param>
    /// <param name="guiEntry">Workbench GUI 入口文件名。</param>
    /// <param name="cliEntry">CLI 入口文件名；未发布 CLI 时为空。</param>
    /// <param name="sharedRuntime">GUI 与 CLI 是否共用运行时文件。</param>
    /// <returns>合并后的运行副本 manifest。</returns>
    public RuntimeManifest Build(
        string runtimeRoot,
        string product,
        RuntimeManifest? existingManifest,
        string platform,
        string guiEntry,
        string cliEntry,
        bool sharedRuntime)
    {
        var currentManifest = BuildInternal(runtimeRoot, product, platform, guiEntry, cliEntry, sharedRuntime);
        if (existingManifest == null)
        {
            return currentManifest;
        }

        var currentPlatform = currentManifest.Platforms[0];
        var platforms = existingManifest.Platforms
            .Where(item => !string.Equals(item.Platform, currentPlatform.Platform, StringComparison.Ordinal))
            .Where(item => IsPublishedPlatformAvailable(runtimeRoot, item))
            .Append(currentPlatform)
            .OrderBy(static item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var layoutVersion = platforms.Any(static item => !string.IsNullOrWhiteSpace(item.CliEntry))
            ? DUAL_ENTRY_LAYOUT_VERSION
            : LEGACY_LAYOUT_VERSION;

        return new RuntimeManifest(
            MANIFEST_VERSION,
            layoutVersion,
            product,
            DateTimeOffset.UtcNow,
            PORTABLE_RUNTIME_ROOT,
            platforms);
    }

    /// <summary>
    /// 执行 manifest 生成；CLI 入口存在时使用双入口布局，sharedRuntime 只描述是否共用运行时文件。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="product">产品名。</param>
    /// <param name="platform">平台标识。</param>
    /// <param name="guiEntry">Workbench GUI 入口文件名。</param>
    /// <param name="cliEntry">轻量 CLI 入口文件名。</param>
    /// <param name="sharedRuntime">GUI 与 CLI 是否共用运行时文件。</param>
    /// <returns>运行副本 manifest。</returns>
    private static RuntimeManifest BuildInternal(
        string runtimeRoot,
        string product,
        string platform,
        string guiEntry,
        string cliEntry,
        bool sharedRuntime)
    {
        var fullRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var platformRoot = RuntimePathGuard.RequirePlatformRoot(fullRuntimeRoot, platform);
        if (!Directory.Exists(platformRoot))
        {
            throw new DirectoryNotFoundException("Platform runtime directory was not found: " + platformRoot);
        }

        var guiEntryFullPath = RequireRuntimeFile(platformRoot, guiEntry, nameof(guiEntry), "Runtime GUI entrypoint was not found.");
        var hasCli = !string.IsNullOrWhiteSpace(cliEntry);
        if (sharedRuntime && !hasCli)
        {
            throw new ArgumentException("A shared runtime layout requires a CLI entry.", nameof(cliEntry));
        }

        var cliEntryFullPath = string.Empty;
        if (hasCli)
        {
            cliEntryFullPath = RequireRuntimeFile(platformRoot, cliEntry, nameof(cliEntry), "Runtime CLI entrypoint was not found.");
        }

        var files = Directory.EnumerateFiles(platformRoot, "*", SearchOption.AllDirectories)
            .Where(path => IsRuntimePayloadFile(platformRoot, path))
            .Select(path => CreateFileRecord(fullRuntimeRoot, path))
            .OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totalBytes = files.Sum(static file => file.SizeBytes);
        var guiEntryPath = NormalizeRelativePath(Path.GetRelativePath(fullRuntimeRoot, guiEntryFullPath));
        var cliEntryPath = hasCli
            ? NormalizeRelativePath(Path.GetRelativePath(fullRuntimeRoot, cliEntryFullPath))
            : string.Empty;
        var platformManifest = new RuntimePlatformManifest(
            platform,
            platform,
            sharedRuntime,
            guiEntryPath,
            guiEntryPath,
            cliEntryPath,
            files.Length,
            totalBytes,
            files);
        var layoutVersion = hasCli ? DUAL_ENTRY_LAYOUT_VERSION : LEGACY_LAYOUT_VERSION;
        return new RuntimeManifest(MANIFEST_VERSION, layoutVersion, product, DateTimeOffset.UtcNow, PORTABLE_RUNTIME_ROOT, new[] { platformManifest });
    }

    /// <summary>
    /// 检查已有平台记录对应的目录和入口是否仍然存在，避免合并后保留已经裁剪的发布产物。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="platform">已有平台记录。</param>
    /// <returns>平台目录与所有必需入口均存在时返回 true。</returns>
    private static bool IsPublishedPlatformAvailable(string runtimeRoot, RuntimePlatformManifest platform)
    {
        var platformRoot = ResolvePathInsideRuntimeRoot(runtimeRoot, platform.Platform);
        if (platformRoot == null || !Directory.Exists(platformRoot))
        {
            return false;
        }

        var guiEntry = string.IsNullOrWhiteSpace(platform.GuiEntry) ? platform.Entrypoint : platform.GuiEntry;
        if (!IsPublishedEntryAvailable(runtimeRoot, guiEntry))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(platform.CliEntry)
            || IsPublishedEntryAvailable(runtimeRoot, platform.CliEntry);
    }

    /// <summary>
    /// 检查 manifest 相对入口是否仍位于 runtime root 内且文件存在。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="entry">manifest 相对入口。</param>
    /// <returns>入口合法且存在时返回 true。</returns>
    private static bool IsPublishedEntryAvailable(string runtimeRoot, string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        var entryPath = ResolvePathInsideRuntimeRoot(runtimeRoot, entry);
        return entryPath != null && File.Exists(entryPath);
    }

    /// <summary>
    /// 将 manifest 相对路径解析到 runtime root 内，拒绝绝对路径和目录穿越。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="relativePath">待解析相对路径。</param>
    /// <returns>合法完整路径；路径越界时返回 null。</returns>
    private static string? ResolvePathInsideRuntimeRoot(string runtimeRoot, string relativePath)
    {
        return RuntimePathGuard.TryResolveInside(runtimeRoot, relativePath, out var fullPath)
            ? fullPath
            : null;
    }

    /// <summary>
    /// 确认发布入口文件存在；缺失时抛出带完整路径的异常，方便脚本输出证据。
    /// </summary>
    /// <param name="platformRoot">平台运行时目录。</param>
    /// <param name="entry">入口文件名。</param>
    /// <param name="parameterName">入口参数名。</param>
    /// <param name="message">缺失时的错误说明。</param>
    /// <returns>平台目录内的入口完整路径。</returns>
    private static string RequireRuntimeFile(string platformRoot, string entry, string parameterName, string message)
    {
        var entryPath = RuntimePathGuard.RequireEntryPath(platformRoot, entry, parameterName);
        if (!File.Exists(entryPath))
        {
            throw new FileNotFoundException(message, entryPath);
        }

        return entryPath;
    }

    /// <summary>
    /// 判断文件是否应进入运行时 manifest；调试符号不参与运行时校验，避免大型 PDB 被视为交付产物。
    /// </summary>
    /// <param name="platformRoot">当前平台根，用于限定运行态目录判断边界。</param>
    /// <param name="path">待检查文件路径。</param>
    /// <returns>应进入 manifest 时返回 true。</returns>
    private static bool IsRuntimePayloadFile(string platformRoot, string path)
    {
        return !string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase)
            && !ContainsRelativeDirectory(platformRoot, path, RUNTIME_STATE_DIRECTORY_NAME);
    }

    /// <summary>
    /// 判断文件相对平台根的祖先目录是否包含指定名称，避免把项目级 `.yokiframe` 祖先误判成平台内状态目录。
    /// </summary>
    /// <param name="platformRoot">平台根目录。</param>
    /// <param name="path">待检查文件路径。</param>
    /// <param name="directoryName">需要排除的目录名。</param>
    /// <returns>平台内相对祖先目录名称匹配时返回 true。</returns>
    private static bool ContainsRelativeDirectory(string platformRoot, string path, string directoryName)
    {
        var relativePath = Path.GetRelativePath(platformRoot, path);
        ReadOnlySpan<char> remaining = relativePath.AsSpan();
        while (true)
        {
            int sep = remaining.IndexOfAny('/', '\\');
            if (sep < 0) return false;
            var segment = remaining[..sep];
            if (segment.Equals(directoryName.AsSpan(), StringComparison.OrdinalIgnoreCase)) return true;
            remaining = remaining[(sep + 1)..];
        }
    }

    /// <summary>
    /// 创建单个文件的 manifest 记录。
    /// </summary>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="path">文件路径。</param>
    /// <returns>文件记录。</returns>
    private static RuntimeManifestFile CreateFileRecord(string runtimeRoot, string path)
    {
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(runtimeRoot, path));
        var info = new FileInfo(path);
        return new RuntimeManifestFile(relativePath, info.Length, ComputeSha256(path));
    }

    /// <summary>
    /// 计算文件 SHA256。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <returns>十六进制 SHA256。</returns>
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 统一 manifest 中的相对路径分隔符。
    /// </summary>
    /// <param name="path">相对路径。</param>
    /// <returns>使用正斜杠的相对路径。</returns>
    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
