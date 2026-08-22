using System.Security.Cryptography;
using System.Text.Json;

namespace YokiFrame.RuntimeCache;

/// <summary>
/// 校验 Runtime manifest 文件摘要、逐文件哈希与平台目录物理文件集合。
/// </summary>
public static class RuntimeManifestFileSetValidator
{
    /// <summary>
    /// 验证平台文件计数、总大小、逐文件内容和额外载荷。
    /// </summary>
    /// <param name="platform">目标平台 JSON。</param>
    /// <param name="runtimeRoot">Runtime 根。</param>
    /// <param name="platformRoot">目标平台根。</param>
    /// <param name="validateContentHashes">是否读取文件内容并校验 SHA-256；启动快速路径传入 false。</param>
    /// <param name="files">验证后的文件完整路径集合。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>清单与物理载荷完全一致时返回 true。</returns>
    public static bool TryValidate(
        JsonElement platform,
        string runtimeRoot,
        string platformRoot,
        bool validateContentHashes,
        out HashSet<string> files,
        out string error)
    {
        files = new HashSet<string>(RuntimeManifestPathPolicy.PathComparer);
        error = string.Empty;
        if (!RuntimeManifestJson.TryReadInt32(platform, "fileCount", out var fileCount) || fileCount < 0
            || !RuntimeManifestJson.TryReadInt64(platform, "totalBytes", out var totalBytes) || totalBytes < 0
            || !platform.TryGetProperty("files", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            error = "Runtime manifest file summary is invalid.";
            return false;
        }

        long calculatedBytes = 0;
        foreach (var record in records.EnumerateArray())
        {
            if (!TryValidateFileRecord(
                    record,
                    runtimeRoot,
                    platformRoot,
                    validateContentHashes,
                    files,
                    ref calculatedBytes,
                    out error))
            {
                return false;
            }
        }

        if (files.Count != fileCount || calculatedBytes != totalBytes)
        {
            error = "Runtime manifest file count or total size does not match its records.";
            return false;
        }

        return TryValidateActualFileSet(platformRoot, files, out error);
    }

    /// <summary>
    /// 验证单个文件记录位于目标平台并与磁盘长度和哈希一致。
    /// </summary>
    /// <param name="record">文件记录 JSON。</param>
    /// <param name="runtimeRoot">Runtime 根。</param>
    /// <param name="platformRoot">目标平台根。</param>
    /// <param name="validateContentHashes">是否读取文件内容并校验 SHA-256。</param>
    /// <param name="files">已验证文件集合。</param>
    /// <param name="calculatedBytes">累计文件大小。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>文件记录可信时返回 true。</returns>
    private static bool TryValidateFileRecord(
        JsonElement record,
        string runtimeRoot,
        string platformRoot,
        bool validateContentHashes,
        ISet<string> files,
        ref long calculatedBytes,
        out string error)
    {
        error = string.Empty;
        if (!RuntimeManifestJson.TryReadString(record, "relativePath", out var relativePath)
            || !RuntimeManifestJson.TryReadInt64(record, "sizeBytes", out var sizeBytes) || sizeBytes < 0
            || !RuntimeManifestJson.TryReadString(record, "sha256", out var sha256) || !IsSha256(sha256)
            || !RuntimeManifestPathPolicy.TryResolveFileInside(runtimeRoot, relativePath, out var fullPath)
            || !RuntimeManifestPathPolicy.IsInside(platformRoot, fullPath)
            || !RuntimeManifestPathPolicy.IsRuntimePayloadFile(platformRoot, fullPath))
        {
            error = "Runtime manifest contains an invalid file record.";
            return false;
        }

        var actualSize = new FileInfo(fullPath).Length;
        if (actualSize != sizeBytes
            || validateContentHashes
                && !string.Equals(ComputeSha256(fullPath), sha256, StringComparison.OrdinalIgnoreCase)
            || !files.Add(fullPath))
        {
            error = "Runtime manifest file size, hash, or path uniqueness check failed.";
            return false;
        }

        calculatedBytes = checked(calculatedBytes + actualSize);
        return true;
    }

    /// <summary>
    /// 确认平台目录没有遗漏、额外或通过链接转向外部的发布载荷。
    /// </summary>
    /// <param name="platformRoot">目标平台根。</param>
    /// <param name="manifestFiles">manifest 声明的完整路径集合。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>物理载荷集合完全一致时返回 true。</returns>
    private static bool TryValidateActualFileSet(
        string platformRoot,
        IReadOnlySet<string> manifestFiles,
        out string error)
    {
        if (!TryCollectActualFiles(platformRoot, out var actualFiles, out error))
        {
            return false;
        }

        if (!actualFiles.SetEquals(manifestFiles))
        {
            error = "Runtime profile files do not match the manifest file set.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 不跟随 reparse point 遍历平台目录，收集实际发布载荷并显式拒绝链接项。
    /// </summary>
    /// <param name="platformRoot">目标平台根。</param>
    /// <param name="files">实际载荷完整路径集合。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>遍历期间未发现链接项时返回 true。</returns>
    private static bool TryCollectActualFiles(
        string platformRoot,
        out HashSet<string> files,
        out string error)
    {
        files = new HashSet<string>(RuntimeManifestPathPolicy.PathComparer);
        error = string.Empty;
        var pending = new Stack<string>();
        pending.Push(platformRoot);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (RuntimeManifestPathPolicy.IsReparsePoint(directory))
                {
                    error = "Runtime profile contains a symbolic link or reparse-point directory.";
                    return false;
                }

                if (!RuntimeManifestPathPolicy.IsRuntimeStateDirectory(directory))
                {
                    pending.Push(directory);
                }
            }

            foreach (var path in Directory.EnumerateFiles(current))
            {
                if (RuntimeManifestPathPolicy.IsReparsePoint(path))
                {
                    error = "Runtime profile contains a symbolic link or reparse-point file.";
                    return false;
                }

                if (RuntimeManifestPathPolicy.IsRuntimePayloadFile(platformRoot, path))
                {
                    files.Add(Path.GetFullPath(path));
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 验证 SHA-256 十六进制文本格式。
    /// </summary>
    /// <param name="value">待验证文本。</param>
    /// <returns>恰好包含 64 个十六进制字符时返回 true。</returns>
    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(static character => Uri.IsHexDigit(character));
    }

    /// <summary>
    /// 计算文件 SHA-256 十六进制文本。
    /// </summary>
    /// <param name="path">目标文件。</param>
    /// <returns>小写 SHA-256。</returns>
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
