using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 EventKit 源码定位的项目路径保护和显式 UserAction 命令。</summary>
public sealed partial class WorkbenchDashboardService
{
    private const string OPEN_CODE_LOCATION_ACTION = "open_code_location";

    /// <summary>校验项目内 C# 位置并请求当前宿主使用配置的代码编辑器打开。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="location">静态扫描返回的项目相对位置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task OpenEventKitCodeLocationAsync(
        string engineId,
        WorkbenchEventKitCodeLocation location,
        CancellationToken cancellationToken)
    {
        string relativePath = ValidateCodeLocation(location);
        await OpenCodeLocationAsync(engineId, relativePath, location.Line, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>规范化 ResKit 调试来源并请求当前宿主打开项目内代码位置。</summary>
    public async Task OpenResKitCodeLocationAsync(
        string engineId,
        string filePath,
        int line,
        CancellationToken cancellationToken)
    {
        string relativePath = ValidateCodeLocation(filePath, line);
        await OpenCodeLocationAsync(engineId, relativePath, line, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>规范化 PoolKit 借出位置并请求当前宿主打开项目内代码位置。</summary>
    /// <param name="engineId">目标宿主 engine。</param>
    /// <param name="filePath">堆栈采集到的相对或绝对源码路径。</param>
    /// <param name="line">一基源码行号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>宿主完成打开请求后的任务；路径无效或宿主拒绝时抛出异常。</returns>
    public async Task OpenPoolKitCodeLocationAsync(
        string engineId,
        string filePath,
        int line,
        CancellationToken cancellationToken)
    {
        string relativePath = ValidateCodeLocation(filePath, line);
        await OpenCodeLocationAsync(engineId, relativePath, line, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>通过可靠 FileBridge UserAction 请求宿主打开已校验的项目相对代码位置。</summary>
    private async Task OpenCodeLocationAsync(
        string engineId,
        string relativePath,
        int line,
        CancellationToken cancellationToken)
    {
        string payloadJson = JsonSerializer.Serialize(
            new CodeLocationPayload(relativePath, line),
            DashboardJsonContext.Default.CodeLocationPayload);
        WorkbenchCommandState result = await SendCommandAsync(
            engineId,
            SYSTEM_KIT,
            OPEN_CODE_LOCATION_ACTION,
            payloadJson,
            cancellationToken).ConfigureAwait(false);
        if (!result.Ok)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "宿主未能打开代码位置。"
                : result.ErrorMessage);
        }
    }

    /// <summary>验证相对路径、Assets containment、扩展名和文件存在性。</summary>
    private string ValidateCodeLocation(WorkbenchEventKitCodeLocation location)
    {
        if (location == null || string.IsNullOrWhiteSpace(location.FilePath))
        {
            throw new ArgumentException("Code location is required.", nameof(location));
        }

        if (Path.IsPathRooted(location.FilePath))
        {
            throw new InvalidOperationException("Code location must be a project-relative C# file.");
        }

        return ValidateCodeLocation(location.FilePath, location.Line);
    }

    /// <summary>规范化相对或绝对代码路径，并验证 Assets containment、扩展名、行号和文件存在性。</summary>
    private string ValidateCodeLocation(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(filePath) || line <= 0)
        {
            throw new ArgumentException("Code location requires one file path and positive line.");
        }

        string normalizedPath = filePath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.IsPathRooted(normalizedPath)
            ? Path.GetFullPath(normalizedPath)
            : Path.GetFullPath(Path.Combine(mClient.Paths.ProjectRoot, normalizedPath));
        if (!string.Equals(Path.GetExtension(fullPath), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Code location must resolve to a C# file.");
        }

        string assetsRoot = Path.GetFullPath(Path.Combine(mClient.Paths.ProjectRoot, "Assets"));
        string assetsRelative = Path.GetRelativePath(assetsRoot, fullPath);
        if (assetsRelative == ".."
            || assetsRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(assetsRelative)
            || !File.Exists(fullPath))
        {
            throw new InvalidOperationException("Code location must resolve to an existing file inside Assets.");
        }

        return Path.GetRelativePath(mClient.Paths.ProjectRoot, fullPath).Replace('\\', '/');
    }

    /// <summary>定义发送给宿主的最小源码位置 payload。</summary>
    private sealed record CodeLocationPayload(
        [property: JsonPropertyName("filePath")] string FilePath,
        [property: JsonPropertyName("line")] int Line);

    /// <summary>为代码定位命令提供 Native AOT 可用的 JSON 元数据。</summary>
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(CodeLocationPayload))]
    private sealed partial class DashboardJsonContext : JsonSerializerContext
    {
    }
}
