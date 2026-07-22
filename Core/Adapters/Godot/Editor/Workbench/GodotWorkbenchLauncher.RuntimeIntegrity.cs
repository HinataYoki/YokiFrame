#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace YokiFrame
{
    internal static partial class GodotWorkbenchLauncher
    {
        private const int RUNTIME_MANIFEST_VERSION = 1;
        private const int LEGACY_RUNTIME_LAYOUT_VERSION = 1;
        private const int DUAL_ENTRY_RUNTIME_LAYOUT_VERSION = 2;
        private const long MAX_RUNTIME_MANIFEST_BYTES = 16L * 1024L * 1024L;
        private const int MAX_RUNTIME_MANIFEST_FILES = 100000;

        /// <summary>
        /// 验证当前平台 Runtime manifest、完整文件集、文件摘要和入口路径。
        /// </summary>
        /// <param name="manifestPath">Runtime manifest 完整路径。</param>
        /// <param name="runtimeRoot">当前源码指纹对应的 Runtime 根。</param>
        /// <param name="runtimeIds">当前宿主按优先级排列的平台 profile。</param>
        /// <param name="executablePath">验证成功后的 GUI 入口完整路径。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>缓存完整且入口可信时返回 true。</returns>
        private static bool TryValidateRuntimeManifest(
            string manifestPath,
            string runtimeRoot,
            string[] runtimeIds,
            out string executablePath,
            out string error)
        {
            executablePath = string.Empty;
            error = string.Empty;
            try
            {
                if (!TryReadRuntimeManifest(manifestPath, runtimeRoot, out var document, out error))
                {
                    return false;
                }

                using (document)
                {
                    return TryValidateRuntimeDocument(
                        document.RootElement,
                        runtimeRoot,
                        runtimeIds,
                        out executablePath,
                        out error);
                }
            }
            catch (Exception exception) when (IsRecoverableRuntimeValidationFailure(exception))
            {
                executablePath = string.Empty;
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 在受限大小内读取 manifest，并拒绝 manifest 自身或祖先路径为链接。
        /// </summary>
        /// <param name="manifestPath">manifest 完整路径。</param>
        /// <param name="runtimeRoot">Runtime 根。</param>
        /// <param name="document">成功时返回 JSON 文档。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>manifest 可安全读取时返回 true。</returns>
        private static bool TryReadRuntimeManifest(
            string manifestPath,
            string runtimeRoot,
            out JsonDocument document,
            out string error)
        {
            document = null;
            error = string.Empty;
            var manifestInfo = new FileInfo(manifestPath);
            if (!manifestInfo.Exists
                || !IsInside(runtimeRoot, manifestPath)
                || HasReparsePointInPath(runtimeRoot, manifestPath))
            {
                error = "Runtime manifest is missing or uses a symbolic link.";
                return false;
            }

            if (manifestInfo.Length <= 0 || manifestInfo.Length > MAX_RUNTIME_MANIFEST_BYTES)
            {
                error = "Runtime manifest size is invalid.";
                return false;
            }

            using (var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 64 });
            }

            return true;
        }

        /// <summary>
        /// 验证 manifest 头、目标平台、物理文件全集以及 GUI/CLI 入口。
        /// </summary>
        /// <param name="root">manifest 根元素。</param>
        /// <param name="runtimeRoot">Runtime 根。</param>
        /// <param name="runtimeIds">候选平台 profile。</param>
        /// <param name="executablePath">可信 GUI 入口。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>所有门禁通过时返回 true。</returns>
        private static bool TryValidateRuntimeDocument(
            JsonElement root,
            string runtimeRoot,
            string[] runtimeIds,
            out string executablePath,
            out string error)
        {
            executablePath = string.Empty;
            if (!TryValidateManifestHeader(root, out var layoutVersion, out error)
                || !TryFindRuntimePlatform(root, runtimeIds, out var platform, out var runtimeId, out error)
                || !TryValidatePlatformFiles(runtimeRoot, runtimeId, platform, out var files, out error))
            {
                return false;
            }

            return TryValidateRuntimeEntries(
                platform,
                runtimeRoot,
                layoutVersion,
                files,
                out executablePath,
                out error);
        }

        /// <summary>
        /// 校验 manifest 版本、布局版本与可搬运 Runtime 根标记。
        /// </summary>
        /// <param name="root">manifest 根元素。</param>
        /// <param name="layoutVersion">已解析布局版本。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>头部结构符合当前契约时返回 true。</returns>
        private static bool TryValidateManifestHeader(JsonElement root, out int layoutVersion, out string error)
        {
            layoutVersion = ReadInt32(root, "layoutVersion");
            error = string.Empty;
            if (root.ValueKind != JsonValueKind.Object
                || ReadInt32(root, "manifestVersion") != RUNTIME_MANIFEST_VERSION
                || layoutVersion != LEGACY_RUNTIME_LAYOUT_VERSION
                    && layoutVersion != DUAL_ENTRY_RUNTIME_LAYOUT_VERSION
                || !string.Equals(ReadString(root, "runtimeRoot"), ".", StringComparison.Ordinal))
            {
                error = "Runtime manifest header is invalid.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 按启动优先级定位唯一目标平台，并验证 profile 标识一致。
        /// </summary>
        /// <param name="root">manifest 根元素。</param>
        /// <param name="runtimeIds">候选平台 profile。</param>
        /// <param name="platform">唯一平台记录。</param>
        /// <param name="runtimeId">匹配的平台 profile。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>找到唯一且一致的平台记录时返回 true。</returns>
        private static bool TryFindRuntimePlatform(
            JsonElement root,
            string[] runtimeIds,
            out JsonElement platform,
            out string runtimeId,
            out string error)
        {
            platform = default;
            runtimeId = string.Empty;
            error = string.Empty;
            if (runtimeIds == null || runtimeIds.Length == 0
                || !root.TryGetProperty("platforms", out var platforms)
                || platforms.ValueKind != JsonValueKind.Array)
            {
                error = "Runtime manifest platform candidates or records are missing.";
                return false;
            }

            for (var index = 0; index < runtimeIds.Length; index++)
            {
                if (TrySelectRuntimePlatform(platforms, runtimeIds[index], out platform, out var count) && count == 1)
                {
                    runtimeId = runtimeIds[index];
                    if (string.Equals(ReadString(platform, "runtimeIdentifier"), runtimeId, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    error = "Runtime manifest profile identifier does not match its platform.";
                    return false;
                }

                if (count > 1)
                {
                    error = "Runtime manifest contains duplicate target profiles.";
                    return false;
                }
            }

            error = "Runtime manifest has no entry for the current platform.";
            return false;
        }

        /// <summary>
        /// 在平台数组中统计并返回目标 profile 的最后一个匹配记录。
        /// </summary>
        /// <param name="platforms">manifest 平台数组。</param>
        /// <param name="runtimeId">目标 profile。</param>
        /// <param name="platform">匹配记录。</param>
        /// <param name="count">匹配数量。</param>
        /// <returns>至少存在一个匹配记录时返回 true。</returns>
        private static bool TrySelectRuntimePlatform(
            JsonElement platforms,
            string runtimeId,
            out JsonElement platform,
            out int count)
        {
            platform = default;
            count = 0;
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return false;
            }

            foreach (var candidate in platforms.EnumerateArray())
            {
                if (string.Equals(ReadString(candidate, "platform"), runtimeId, StringComparison.Ordinal))
                {
                    platform = candidate;
                    count++;
                }
            }

            return count > 0;
        }

        /// <summary>
        /// 验证平台文件摘要、逐文件哈希和物理文件全集。
        /// </summary>
        /// <param name="runtimeRoot">Runtime 根。</param>
        /// <param name="runtimeId">目标平台 profile。</param>
        /// <param name="platform">目标平台记录。</param>
        /// <param name="files">验证后的完整文件集合。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>manifest 与平台目录完全一致时返回 true。</returns>
        private static bool TryValidatePlatformFiles(
            string runtimeRoot,
            string runtimeId,
            JsonElement platform,
            out HashSet<string> files,
            out string error)
        {
            files = new HashSet<string>(sRuntimePathComparer);
            if (!TryResolveDirectoryInside(runtimeRoot, runtimeId, out var platformRoot))
            {
                error = "Runtime profile directory is missing or uses a symbolic link.";
                return false;
            }

            if (!TryReadFileSummary(platform, out var records, out var fileCount, out var totalBytes, out error))
            {
                return false;
            }

            long calculatedBytes = 0;
            foreach (var record in records.EnumerateArray())
            {
                if (!TryValidateFileRecord(
                        record,
                        runtimeRoot,
                        platformRoot,
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
        /// 读取并约束平台文件计数、总大小与记录数组。
        /// </summary>
        /// <param name="platform">目标平台记录。</param>
        /// <param name="records">文件记录数组。</param>
        /// <param name="fileCount">声明文件数量。</param>
        /// <param name="totalBytes">声明总大小。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>摘要字段严格有效时返回 true。</returns>
        private static bool TryReadFileSummary(
            JsonElement platform,
            out JsonElement records,
            out int fileCount,
            out long totalBytes,
            out string error)
        {
            records = default;
            fileCount = ReadInt32(platform, "fileCount");
            totalBytes = ReadInt64(platform, "totalBytes");
            error = string.Empty;
            if (fileCount < 0 || fileCount > MAX_RUNTIME_MANIFEST_FILES || totalBytes < 0
                || !platform.TryGetProperty("files", out records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() != fileCount)
            {
                error = "Runtime manifest file summary is invalid.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 验证单个文件记录位于目标平台并与磁盘长度和哈希一致。
        /// </summary>
        /// <param name="record">文件记录。</param>
        /// <param name="runtimeRoot">Runtime 根。</param>
        /// <param name="platformRoot">平台根。</param>
        /// <param name="files">已验证文件集合。</param>
        /// <param name="calculatedBytes">累计文件大小。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>文件记录可信时返回 true。</returns>
        private static bool TryValidateFileRecord(
            JsonElement record,
            string runtimeRoot,
            string platformRoot,
            ISet<string> files,
            ref long calculatedBytes,
            out string error)
        {
            var relativePath = ReadString(record, "relativePath");
            var sizeBytes = ReadInt64(record, "sizeBytes");
            var sha256 = ReadString(record, "sha256");
            if (sizeBytes < 0 || !IsSha256(sha256)
                || !TryResolveFileInside(runtimeRoot, relativePath, out var fullPath)
                || !IsInside(platformRoot, fullPath)
                || !IsRuntimePayloadFile(platformRoot, fullPath))
            {
                error = "Runtime manifest contains an invalid file record.";
                return false;
            }

            var actualSize = new FileInfo(fullPath).Length;
            if (actualSize != sizeBytes
                || !string.Equals(ComputeSha256(fullPath), sha256, StringComparison.OrdinalIgnoreCase)
                || !files.Add(fullPath))
            {
                error = "Runtime manifest file size, hash, or path uniqueness check failed.";
                return false;
            }

            calculatedBytes = checked(calculatedBytes + actualSize);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 验证 GUI/CLI 入口已经位于完整性校验通过的文件集合中。
        /// </summary>
        /// <param name="platform">目标平台记录。</param>
        /// <param name="runtimeRoot">Runtime 根。</param>
        /// <param name="layoutVersion">manifest 布局版本。</param>
        /// <param name="files">可信文件集合。</param>
        /// <param name="executablePath">可信 GUI 入口。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>所有声明入口可信时返回 true。</returns>
        private static bool TryValidateRuntimeEntries(
            JsonElement platform,
            string runtimeRoot,
            int layoutVersion,
            ISet<string> files,
            out string executablePath,
            out string error)
        {
            var guiEntry = ReadString(platform, "guiEntry");
            guiEntry = string.IsNullOrWhiteSpace(guiEntry) ? ReadString(platform, "entrypoint") : guiEntry;
            var cliEntry = ReadString(platform, "cliEntry");
            if (!TryResolveListedEntry(runtimeRoot, guiEntry, files, out executablePath)
                || !string.IsNullOrWhiteSpace(cliEntry) && layoutVersion != DUAL_ENTRY_RUNTIME_LAYOUT_VERSION
                || !string.IsNullOrWhiteSpace(cliEntry)
                    && !TryResolveListedEntry(runtimeRoot, cliEntry, files, out _))
            {
                executablePath = string.Empty;
                error = "Runtime manifest GUI or CLI entry is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 读取对象中的可选 64 位整数属性，缺失或类型不符时返回 -1。
        /// </summary>
        /// <param name="element">目标 JSON 对象。</param>
        /// <param name="propertyName">整数属性名称。</param>
        /// <returns>已解析整数；无效时返回 -1。</returns>
        private static long ReadInt64(JsonElement element, string propertyName)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var value)
                && value.TryGetInt64(out var number)
                    ? number
                    : -1L;
        }

        /// <summary>
        /// 计算文件 SHA-256 十六进制文本。
        /// </summary>
        /// <param name="path">目标文件。</param>
        /// <returns>小写 SHA-256。</returns>
        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(stream);
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// 验证 SHA-256 十六进制文本格式。
        /// </summary>
        /// <param name="value">待验证文本。</param>
        /// <returns>恰好包含 64 个十六进制字符时返回 true。</returns>
        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isDigit = character >= '0' && character <= '9';
                var isLowerHex = character >= 'a' && character <= 'f';
                var isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 识别完整性校验期间可转换为缓存无效诊断的异常。
        /// </summary>
        /// <param name="exception">校验异常。</param>
        /// <returns>属于文件、JSON、路径、摘要或数值边界失败时返回 true。</returns>
        private static bool IsRecoverableRuntimeValidationFailure(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is ArgumentException
                || exception is CryptographicException
                || exception is OverflowException;
        }
    }
}
#endif
