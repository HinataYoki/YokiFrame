using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 文件桥的底层文件操作。所有协议文件都应先写临时文件，再原子提交到最终路径。
    /// </summary>
    public static class FileBridgeFileSystem
    {
        /// <summary>
        /// 获取协议临时文件扩展名。
        /// </summary>
        public const string TEMP_EXTENSION = ".tmp";

        /// <summary>
        /// 判断候选路径是否位于指定根目录内。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="candidatePath">待检查的候选路径。</param>
        /// <returns>候选路径位于根目录内时返回 true，否则返回 false。</returns>
        public static bool IsPathWithinRoot(string rootPath, string candidatePath)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(candidatePath))
                return false;

            try
            {
                var root = TrimTrailingSeparators(Path.GetFullPath(rootPath));
                var candidate = TrimTrailingSeparators(Path.GetFullPath(candidatePath));
                var comparison = GetPathComparison();
                if (string.Equals(root, candidate, comparison))
                    return true;

                var rootWithSeparator = root + Path.DirectorySeparatorChar;
                return candidate.StartsWith(rootWithSeparator, comparison);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断候选路径是否位于根目录内，且现有路径片段不包含 reparse point。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="candidatePath">待检查的候选路径。</param>
        /// <returns>路径安全时返回 true，否则返回 false。</returns>
        public static bool IsPathSafeWithinRoot(string rootPath, string candidatePath)
        {
            try
            {
                EnsurePathWithinRoot(rootPath, candidatePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 确保候选路径位于根目录内，且现有路径片段不包含 reparse point。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="candidatePath">待检查的候选路径。</param>
        public static void EnsurePathWithinRoot(string rootPath, string candidatePath)
        {
            if (!IsPathWithinRoot(rootPath, candidatePath))
                throw new InvalidOperationException("FileBridge path escapes root: " + candidatePath);

            if (ContainsReparsePointInExistingPath(rootPath, candidatePath))
                throw new InvalidOperationException("FileBridge path contains reparse point: " + candidatePath);
        }

        /// <summary>
        /// 在根目录约束下创建目录。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="targetPath">要创建的目录路径。</param>
        public static void CreateDirectoryInRoot(string rootPath, string targetPath)
        {
            EnsurePathWithinRoot(rootPath, targetPath);
            Directory.CreateDirectory(targetPath);
            EnsurePathWithinRoot(rootPath, targetPath);
        }

        /// <summary>
        /// 在根目录约束下枚举目录内匹配的文件。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="dir">要枚举的目录。</param>
        /// <param name="pattern">文件匹配模式。</param>
        /// <returns>位于根目录内且通过 reparse point 检查的文件路径。</returns>
        public static string[] GetFilesInRoot(string rootPath, string dir, string pattern)
        {
            EnsurePathWithinRoot(rootPath, dir);
            var files = Directory.GetFiles(dir, pattern);
            var safeFiles = new List<string>(files.Length);
            for (var i = 0; i < files.Length; i++)
            {
                if (IsPathSafeWithinRoot(rootPath, files[i]))
                    safeFiles.Add(files[i]);
            }

            return safeFiles.ToArray();
        }

        /// <summary>
        /// 在根目录约束下读取文本文件。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="path">要读取的文件路径。</param>
        /// <returns>文件内容。</returns>
        public static string ReadAllTextInRoot(string rootPath, string path)
        {
            EnsurePathWithinRoot(rootPath, path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// 在根目录约束下通过临时文件原子写入文本。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="content">要写入的文本。</param>
        public static void AtomicWriteAllTextInRoot(string rootPath, string targetPath, string content)
        {
            EnsurePathWithinRoot(rootPath, targetPath);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                CreateDirectoryInRoot(rootPath, dir);

            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + TEMP_EXTENSION;
            EnsurePathWithinRoot(rootPath, tempPath);
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(content ?? string.Empty);
                    writer.Flush();
                    stream.Flush(true);
                }

                ReplaceFileInRoot(rootPath, tempPath, targetPath);
            }
            catch
            {
                TryDeleteInRoot(rootPath, tempPath);
                throw;
            }
        }

        /// <summary>
        /// 在根目录约束下尝试认领文件。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="targetPath">认领后的目标路径。</param>
        /// <returns>认领成功返回 true；目标已存在或 I/O 失败时返回 false。</returns>
        public static bool TryClaimFileInRoot(string rootPath, string sourcePath, string targetPath)
        {
            try
            {
                EnsurePathWithinRoot(rootPath, sourcePath);
                EnsurePathWithinRoot(rootPath, targetPath);
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    CreateDirectoryInRoot(rootPath, dir);

                if (File.Exists(targetPath))
                    return false;

                File.Move(sourcePath, targetPath);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// 在根目录约束下用源文件替换目标文件。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="targetPath">目标文件路径。</param>
        public static void ReplaceFileInRoot(string rootPath, string sourcePath, string targetPath)
        {
            EnsurePathWithinRoot(rootPath, sourcePath);
            EnsurePathWithinRoot(rootPath, targetPath);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                CreateDirectoryInRoot(rootPath, dir);

            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Move(sourcePath, targetPath);
        }

        /// <summary>
        /// 在根目录约束下追加一行文本并强制刷新到磁盘。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="line">要追加的文本行。</param>
        public static void AppendLineAndFlushInRoot(string rootPath, string targetPath, string line)
        {
            EnsurePathWithinRoot(rootPath, targetPath);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                CreateDirectoryInRoot(rootPath, dir);

            using (var stream = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                var content = line ?? string.Empty;
                writer.Write(content);
                if (!content.EndsWith("\n", StringComparison.Ordinal))
                    writer.Write('\n');
                writer.Flush();
                stream.Flush(true);
            }
        }

        /// <summary>
        /// 在根目录约束下尝试删除文件。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        /// <param name="path">要删除的文件路径。</param>
        public static void TryDeleteInRoot(string rootPath, string path)
        {
            try
            {
                EnsurePathWithinRoot(rootPath, path);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 清理失败不应掩盖原始通信错误。
            }
        }

        /// <summary>
        /// 通过临时文件原子写入文本。
        /// </summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="content">要写入的文本。</param>
        public static void AtomicWriteAllText(string targetPath, string content)
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + TEMP_EXTENSION;
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(content ?? string.Empty);
                    writer.Flush();
                    stream.Flush(true);
                }

                ReplaceFile(tempPath, targetPath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        /// <summary>
        /// 尝试认领文件。
        /// </summary>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="targetPath">认领后的目标路径。</param>
        /// <returns>认领成功返回 true；目标已存在或 I/O 失败时返回 false。</returns>
        public static bool TryClaimFile(string sourcePath, string targetPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(targetPath))
                    return false;

                File.Move(sourcePath, targetPath);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// 用源文件替换目标文件。
        /// </summary>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="targetPath">目标文件路径。</param>
        public static void ReplaceFile(string sourcePath, string targetPath)
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Move(sourcePath, targetPath);
        }

        /// <summary>
        /// 追加一行文本并强制刷新到磁盘。
        /// </summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="line">要追加的文本行。</param>
        public static void AppendLineAndFlush(string targetPath, string line)
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using (var stream = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                var content = line ?? string.Empty;
                writer.Write(content);
                if (!content.EndsWith("\n", StringComparison.Ordinal))
                    writer.Write('\n');
                writer.Flush();
                stream.Flush(true);
            }
        }

        /// <summary>
        /// 尝试删除文件。
        /// </summary>
        /// <param name="path">要删除的文件路径。</param>
        public static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 清理失败不应掩盖原始通信错误。
            }
        }

        private static bool ContainsReparsePointInExistingPath(string rootPath, string candidatePath)
        {
            var root = TrimTrailingSeparators(Path.GetFullPath(rootPath));
            var candidate = TrimTrailingSeparators(Path.GetFullPath(candidatePath));
            var current = (File.Exists(candidate) || Directory.Exists(candidate))
                ? candidate
                : Path.GetDirectoryName(candidate);
            var comparison = GetPathComparison();

            while (!string.IsNullOrEmpty(current) && IsPathWithinRoot(root, current))
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        return true;
                }

                var normalizedCurrent = TrimTrailingSeparators(Path.GetFullPath(current));
                if (string.Equals(normalizedCurrent, root, comparison))
                    break;

                var parent = Path.GetDirectoryName(normalizedCurrent);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, normalizedCurrent, comparison))
                    break;

                current = parent;
            }

            return false;
        }

        private static string TrimTrailingSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var root = Path.GetPathRoot(path) ?? string.Empty;
            while (path.Length > root.Length && IsDirectorySeparator(path[path.Length - 1]))
                path = path.Substring(0, path.Length - 1);

            return path;
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
        }

        private static StringComparison GetPathComparison()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}
