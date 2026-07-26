#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace YokiFrame
{
    internal static partial class YokiFrameWorkbenchLauncher
    {
        private const int RUNTIME_MANIFEST_VERSION = 1;
        private const int RUNTIME_LAYOUT_VERSION = 2;
        private const long MAX_RUNTIME_MANIFEST_BYTES = 16L * 1024L * 1024L;
        private const int MAX_RUNTIME_MANIFEST_FILES = 100000;

        /// <summary>
        /// 快速验证当前平台 Runtime manifest、文件结构和入口路径，避免 Ctrl+E 读取全部 AOT 二进制。
        /// </summary>
        /// <param name="manifestPath">Runtime manifest 完整路径。</param>
        /// <param name="runtimeRoot">当前源码指纹对应的 Runtime 根。</param>
        /// <param name="runtimePlatforms">当前宿主按优先级排列的平台 profile。</param>
        /// <param name="executablePath">验证成功后的 GUI 入口完整路径。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>缓存完整且入口可信时返回 true。</returns>
        private static bool TryValidateRuntimeManifest(
            string manifestPath,
            string runtimeRoot,
            string[] runtimePlatforms,
            out string executablePath,
            out string error)
        {
            executablePath = string.Empty;
            error = string.Empty;
            try
            {
                var manifestInfo = new FileInfo(manifestPath);
                if (!manifestInfo.Exists)
                {
                    error = "Runtime manifest is missing.";
                    return false;
                }

                if (manifestInfo.Length <= 0 || manifestInfo.Length > MAX_RUNTIME_MANIFEST_BYTES)
                {
                    error = "Runtime manifest size is invalid.";
                    return false;
                }

                var manifest = JsonUtility.FromJson<YokiFrameWorkbenchRuntimeManifest>(File.ReadAllText(manifestPath));
                if (!TryValidateManifestHeader(manifest, out error))
                {
                    return false;
                }

                if (!TryFindRuntimePlatform(manifest.platforms, runtimePlatforms, out var platform, out var runtimeProfile, out error))
                {
                    return false;
                }

                if (!TryValidatePlatformFiles(runtimeRoot, runtimeProfile, platform, out var files, out error))
                {
                    return false;
                }

                var guiEntry = string.IsNullOrWhiteSpace(platform.guiEntry)
                    ? platform.entrypoint
                    : platform.guiEntry;
                if (!TryResolveListedEntry(runtimeRoot, guiEntry, files, out executablePath))
                {
                    error = "Runtime manifest GUI entry is invalid or not listed in its file set.";
                    executablePath = string.Empty;
                    return false;
                }

                var cliEntry = platform.cliEntry;
                if (!string.IsNullOrWhiteSpace(cliEntry)
                    && !TryResolveListedEntry(runtimeRoot, cliEntry, files, out _))
                {
                    error = "Runtime manifest CLI entry is invalid or not listed in its file set.";
                    executablePath = string.Empty;
                    return false;
                }

                return true;
            }
            catch (Exception exception) when (IsRecoverableRuntimeValidationFailure(exception))
            {
                executablePath = string.Empty;
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 校验 manifest 版本、可搬运 Runtime 根和平台数组基本结构。
        /// </summary>
        /// <param name="manifest">已解析 manifest。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>头部结构符合当前启动器契约时返回 true。</returns>
        private static bool TryValidateManifestHeader(
            YokiFrameWorkbenchRuntimeManifest manifest,
            out string error)
        {
            error = string.Empty;
            if (manifest == null
                || manifest.manifestVersion != RUNTIME_MANIFEST_VERSION
                || manifest.layoutVersion < 1
                || manifest.layoutVersion > RUNTIME_LAYOUT_VERSION
                || !string.Equals(manifest.runtimeRoot, ".", StringComparison.Ordinal)
                || manifest.platforms == null)
            {
                error = "Runtime manifest header is invalid.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 按启动优先级定位唯一目标平台，拒绝重复 profile 记录。
        /// </summary>
        /// <param name="platforms">manifest 平台数组。</param>
        /// <param name="runtimePlatforms">当前宿主候选 profile。</param>
        /// <param name="platform">匹配的平台记录。</param>
        /// <param name="runtimeProfile">匹配的 profile 名称。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>找到唯一目标平台时返回 true。</returns>
        private static bool TryFindRuntimePlatform(
            YokiFrameWorkbenchRuntimePlatform[] platforms,
            string[] runtimePlatforms,
            out YokiFrameWorkbenchRuntimePlatform platform,
            out string runtimeProfile,
            out string error)
        {
            platform = null;
            runtimeProfile = string.Empty;
            error = string.Empty;
            if (runtimePlatforms == null || runtimePlatforms.Length == 0)
            {
                error = "Runtime profile candidates are empty.";
                return false;
            }

            for (var candidateIndex = 0; candidateIndex < runtimePlatforms.Length; candidateIndex++)
            {
                var candidate = runtimePlatforms[candidateIndex];
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                var matchCount = 0;
                YokiFrameWorkbenchRuntimePlatform match = null;
                for (var platformIndex = 0; platformIndex < platforms.Length; platformIndex++)
                {
                    var current = platforms[platformIndex];
                    if (current != null && string.Equals(current.platform, candidate, StringComparison.Ordinal))
                    {
                        match = current;
                        matchCount++;
                    }
                }

                if (matchCount == 0) continue;
                if (matchCount != 1)
                {
                    error = "Runtime manifest contains duplicate target profiles.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(match.runtimeIdentifier)
                    && !string.Equals(match.runtimeIdentifier, candidate, StringComparison.Ordinal))
                {
                    error = "Runtime manifest profile identifier does not match its platform.";
                    return false;
                }

                platform = match;
                runtimeProfile = candidate;
                return true;
            }

            error = "Runtime manifest has no entry for the current platform.";
            return false;
        }

        /// <summary>
        /// 验证平台目录的文件数量、长度、摘要格式、路径链和实际文件全集。
        /// </summary>
        /// <param name="runtimeRoot">Runtime 根目录。</param>
        /// <param name="runtimeProfile">目标 profile。</param>
        /// <param name="platform">目标平台记录。</param>
        /// <param name="files">已验证的完整文件路径集合。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>平台载荷完整一致时返回 true。</returns>
        private static bool TryValidatePlatformFiles(
            string runtimeRoot,
            string runtimeProfile,
            YokiFrameWorkbenchRuntimePlatform platform,
            out HashSet<string> files,
            out string error)
        {
            files = new HashSet<string>(RuntimePathComparer());
            error = string.Empty;
            if (!TryResolveInside(runtimeRoot, runtimeProfile, out var platformRoot)
                || !Directory.Exists(platformRoot)
                || HasReparsePointInPath(runtimeRoot, platformRoot))
            {
                error = "Runtime profile directory is missing or uses a symbolic link.";
                return false;
            }

            if (platform.files == null
                || platform.fileCount < 0
                || platform.fileCount > MAX_RUNTIME_MANIFEST_FILES
                || platform.fileCount != platform.files.Length
                || platform.totalBytes < 0)
            {
                error = "Runtime manifest file summary is invalid.";
                return false;
            }

            long calculatedBytes = 0;
            // 同一目录前缀在多条记录间反复出现，缓存已验证目录使祖先属性只读一次。
            var verifiedDirectories = new HashSet<string>(RuntimePathComparer());
            for (var index = 0; index < platform.files.Length; index++)
            {
                var record = platform.files[index];
                if (record == null
                    || !TryResolveInside(runtimeRoot, record.relativePath, out var fullPath)
                    || !IsInside(platformRoot, fullPath)
                    || !File.Exists(fullPath)
                    || HasReparsePointInPath(runtimeRoot, fullPath, verifiedDirectories)
                    || IsRuntimeStatePath(platformRoot, fullPath)
                    || string.Equals(Path.GetExtension(fullPath), ".pdb", StringComparison.OrdinalIgnoreCase)
                    || !IsSha256(record.sha256))
                {
                    error = "Runtime manifest contains an invalid file record.";
                    return false;
                }

                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length != record.sizeBytes || !files.Add(fullPath))
                {
                    error = "Runtime manifest file size or path uniqueness check failed.";
                    return false;
                }

                calculatedBytes = checked(calculatedBytes + fileInfo.Length);
            }

            if (calculatedBytes != platform.totalBytes)
            {
                error = "Runtime manifest file total size does not match its records.";
                return false;
            }

            if (!TryCollectActualFiles(platformRoot, files, out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 不跟随目录链接遍历平台目录，并确认实际载荷与 manifest 文件集合完全一致。
        /// </summary>
        /// <remarks>
        /// 维持『父先于子验证』不变量：platformRoot 由 TryValidatePlatformFiles 入口全链验证，
        /// 入栈目录在压栈前已验证非重解析点，故对每个子目录与文件只需检查叶节点自身，
        /// 祖先属性不再重复读取。破坏该不变量会静默削弱防篡改检查面。
        /// </remarks>
        /// <param name="platformRoot">平台目录。</param>
        /// <param name="manifestFiles">已验证 manifest 文件集合。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>实际文件集合一致且没有链接项时返回 true。</returns>
        private static bool TryCollectActualFiles(
            string platformRoot,
            HashSet<string> manifestFiles,
            out string error)
        {
            error = string.Empty;
            var actualFiles = new HashSet<string>(RuntimePathComparer());
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(platformRoot);
            while (pendingDirectories.Count > 0)
            {
                var current = pendingDirectories.Pop();
                foreach (var directory in Directory.GetDirectories(current))
                {
                    if (IsReparsePoint(directory))
                    {
                        error = "Runtime profile contains a symbolic link or reparse-point directory.";
                        return false;
                    }

                    if (!IsRuntimeStatePath(platformRoot, directory))
                    {
                        pendingDirectories.Push(directory);
                    }
                }

                foreach (var path in Directory.GetFiles(current))
                {
                    if (IsReparsePoint(path))
                    {
                        error = "Runtime profile contains a symbolic link or reparse-point file.";
                        return false;
                    }

                    if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    actualFiles.Add(Path.GetFullPath(path));
                }
            }

            if (actualFiles.Count != manifestFiles.Count)
            {
                error = "Runtime profile files do not match the manifest file set.";
                return false;
            }

            foreach (var path in actualFiles)
            {
                if (!manifestFiles.Contains(path))
                {
                    error = "Runtime profile files do not match the manifest file set.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>把 manifest 相对路径解析到根目录内，并拒绝绝对路径和目录穿越。</summary>
        private static bool TryResolveInside(string root, string relativePath, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.StartsWith("/", StringComparison.Ordinal)
                || relativePath.StartsWith("\\", StringComparison.Ordinal)
                || relativePath.Length >= 2 && relativePath[1] == ':')
            {
                return false;
            }

            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, NormalizeRelativePathForRuntime(relativePath)));
            var prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, RuntimePathComparison()))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }

        /// <summary>判断完整路径是否位于根目录后代。</summary>
        private static bool IsInside(string root, string path)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(path);
            return candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, RuntimePathComparison());
        }

        /// <summary>检查根到目标的每一级是否为 reparse point。</summary>
        private static bool HasReparsePointInPath(string root, string path)
        {
            return HasReparsePointInPath(root, path, null);
        }

        /// <summary>
        /// 检查根到目标的每一级是否为 reparse point，并复用已验证目录前缀短路。
        /// </summary>
        /// <param name="root">校验起点目录。</param>
        /// <param name="path">目标完整路径。</param>
        /// <param name="verifiedDirectories">同一轮已验证非重解析点的目录缓存；为空时不复用。</param>
        /// <returns>路径链上存在重解析点时返回 true。</returns>
        private static bool HasReparsePointInPath(
            string root,
            string path,
            HashSet<string> verifiedDirectories)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (verifiedDirectories == null || !verifiedDirectories.Contains(normalizedRoot))
            {
                if (IsReparsePoint(normalizedRoot)) return true;
                if (verifiedDirectories != null) verifiedDirectories.Add(normalizedRoot);
            }

            var relativePath = Path.GetRelativePath(normalizedRoot, Path.GetFullPath(path));
            var current = normalizedRoot;
            var segments = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                bool isLastSegment = index == segments.Length - 1;
                // 只缓存目录前缀；叶节点是文件，缓存后无法被后续记录复用。
                if (!isLastSegment && verifiedDirectories != null && verifiedDirectories.Contains(current))
                {
                    continue;
                }

                if (IsReparsePoint(current)) return true;
                if (!isLastSegment && verifiedDirectories != null) verifiedDirectories.Add(current);
            }

            return false;
        }

        /// <summary>安全读取文件系统项的 reparse 属性；目标必须已存在。</summary>
        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        /// <summary>判断路径是否位于运行态状态目录，避免把可变状态当作发布载荷。</summary>
        private static bool IsRuntimeStatePath(string platformRoot, string path)
        {
            var relative = Path.GetRelativePath(platformRoot, path).Replace('\\', '/');
            var segments = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                if (string.Equals(segments[index], ".yokiframe", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        /// <summary>验证 manifest 中的 SHA-256 文本格式。</summary>
        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isDigit = character >= '0' && character <= '9';
                var isLowerHex = character >= 'a' && character <= 'f';
                var isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex) return false;
            }

            return true;
        }

        /// <summary>验证并返回已列入 manifest 文件集的入口路径。</summary>
        private static bool TryResolveListedEntry(
            string runtimeRoot,
            string entry,
            HashSet<string> files,
            out string fullPath)
        {
            return TryResolveInside(runtimeRoot, entry, out fullPath)
                && File.Exists(fullPath)
                && !HasReparsePointInPath(runtimeRoot, fullPath)
                && files.Contains(fullPath);
        }

        /// <summary>将当前宿主转换为路径比较规则。</summary>
        private static StringComparison RuntimePathComparison()
        {
            return Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        /// <summary>获取与当前 Unity Editor 文件系统路径规则一致的集合比较器。</summary>
        private static StringComparer RuntimePathComparer()
        {
            return Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        /// <summary>把 manifest 路径分隔符转换为当前 Unity Editor 的系统分隔符。</summary>
        private static string NormalizeRelativePathForRuntime(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>统一处理 Unity 文件系统校验中可恢复的损坏或访问异常。</summary>
        private static bool IsRecoverableRuntimeValidationFailure(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is InvalidDataException
                || exception is CryptographicException;
        }
    }
}

#endif
