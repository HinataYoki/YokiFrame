using System.Text.Json;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 读取已有 WorkbenchRuntime manifest，供跨平台发布时合并平台记录。
/// </summary>
public sealed class RuntimeManifestReader
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 如果 manifest 文件存在，则读取并反序列化；不存在时返回 null。
    /// </summary>
    /// <param name="path">manifest 文件路径。</param>
    /// <returns>已有 manifest；文件不存在时返回 null。</returns>
    public RuntimeManifest? ReadIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<RuntimeManifest>(File.ReadAllText(path), ManifestJsonOptions);
            return IsStructurallyValid(manifest) ? manifest : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// 验证反序列化结果包含当前版本、可搬运根和完整平台摘要，防止损坏 manifest 进入合并流程。
    /// </summary>
    /// <param name="manifest">反序列化结果。</param>
    /// <returns>结构可供后续磁盘完整性校验时返回 true。</returns>
    private static bool IsStructurallyValid(RuntimeManifest? manifest)
    {
        if (manifest == null
            || manifest.ManifestVersion != 1
            || manifest.LayoutVersion is not (1 or 2)
            || !string.Equals(manifest.RuntimeRoot, ".", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(manifest.Product)
            || manifest.Platforms == null
            || manifest.Platforms.Count == 0)
        {
            return false;
        }

        return manifest.Platforms.All(IsStructurallyValid);
    }

    /// <summary>
    /// 验证单个平台记录的标识、入口和文件摘要具有可计算结构。
    /// </summary>
    /// <param name="platform">平台记录。</param>
    /// <returns>平台结构完整时返回 true。</returns>
    private static bool IsStructurallyValid(RuntimePlatformManifest platform)
    {
        if (platform == null
            || string.IsNullOrWhiteSpace(platform.Platform)
            || string.IsNullOrWhiteSpace(platform.RuntimeIdentifier)
            || string.IsNullOrWhiteSpace(platform.GuiEntry) && string.IsNullOrWhiteSpace(platform.Entrypoint)
            || platform.FileCount < 0
            || platform.TotalBytes < 0
            || platform.Files == null
            || platform.FileCount != platform.Files.Count)
        {
            return false;
        }

        return HasValidFileSummary(platform);
    }

    /// <summary>
    /// 校验文件记录字段、总大小和相对路径唯一性，不在 Reader 阶段访问物理文件。
    /// </summary>
    /// <param name="platform">平台记录。</param>
    /// <returns>文件摘要内部一致时返回 true。</returns>
    private static bool HasValidFileSummary(RuntimePlatformManifest platform)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0L;
        foreach (var file in platform.Files)
        {
            if (file == null
                || string.IsNullOrWhiteSpace(file.RelativePath)
                || file.SizeBytes < 0
                || file.Sha256?.Length != 64
                || !file.Sha256.All(static character => Uri.IsHexDigit(character))
                || !paths.Add(file.RelativePath))
            {
                return false;
            }

            totalBytes = checked(totalBytes + file.SizeBytes);
        }

        return totalBytes == platform.TotalBytes;
    }
}
