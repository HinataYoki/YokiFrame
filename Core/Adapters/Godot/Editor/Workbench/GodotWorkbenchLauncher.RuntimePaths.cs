#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    internal static partial class GodotWorkbenchLauncher
    {
        private const string RUNTIME_STATE_DIRECTORY_NAME = ".yokiframe";

        private static readonly StringComparer sRuntimePathComparer = Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <summary>
        /// 将相对目录解析到 Runtime 根内，并拒绝不存在或包含链接的目录链。
        /// </summary>
        /// <param name="root">约束根目录。</param>
        /// <param name="relativePath">相对目录路径。</param>
        /// <param name="fullPath">可信目录完整路径。</param>
        /// <returns>目录存在且路径链无 reparse point 时返回 true。</returns>
        private static bool TryResolveDirectoryInside(string root, string relativePath, out string fullPath)
        {
            return TryResolveInside(root, relativePath, out fullPath)
                && Directory.Exists(fullPath)
                && !HasReparsePointInPath(root, fullPath);
        }

        /// <summary>
        /// 将相对文件解析到 Runtime 根内，并拒绝不存在或包含链接的文件链。
        /// </summary>
        /// <param name="root">约束根目录。</param>
        /// <param name="relativePath">相对文件路径。</param>
        /// <param name="fullPath">可信文件完整路径。</param>
        /// <returns>文件存在且路径链无 reparse point 时返回 true。</returns>
        private static bool TryResolveFileInside(string root, string relativePath, out string fullPath)
        {
            return TryResolveInside(root, relativePath, out fullPath)
                && File.Exists(fullPath)
                && !HasReparsePointInPath(root, fullPath);
        }

        /// <summary>
        /// 将跨平台相对路径解析到根目录后代，拒绝绝对路径和目录穿越。
        /// </summary>
        /// <param name="root">约束根目录。</param>
        /// <param name="relativePath">manifest 相对路径。</param>
        /// <param name="fullPath">合法时返回完整路径。</param>
        /// <returns>路径位于根目录后代时返回 true。</returns>
        private static bool TryResolveInside(string root, string relativePath, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath) || IsPortableRooted(relativePath))
            {
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(root, NormalizeForHost(relativePath)));
            if (!IsInside(root, candidate))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }

        /// <summary>
        /// 判断候选完整路径是否为指定根目录的后代。
        /// </summary>
        /// <param name="root">根目录。</param>
        /// <param name="path">候选路径。</param>
        /// <returns>候选位于根目录内且不等于根本身时返回 true。</returns>
        private static bool IsInside(string root, string path)
        {
            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prefix = normalizedRoot + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, RuntimePathComparison());
        }

        /// <summary>
        /// 检查从受控根到目标项的每一级是否包含符号链接、junction 或 reparse point。
        /// </summary>
        /// <param name="root">已存在的受控根目录。</param>
        /// <param name="path">根内已存在的目标路径。</param>
        /// <returns>任一级为链接或 junction 时返回 true。</returns>
        private static bool HasReparsePointInPath(string root, string path)
        {
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (IsReparsePoint(fullRoot))
            {
                return true;
            }

            var current = fullRoot;
            var relativePath = Path.GetRelativePath(fullRoot, Path.GetFullPath(path));
            var segments = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                if (IsReparsePoint(current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断文件系统项是否为符号链接、junction 或其它 reparse point。
        /// </summary>
        /// <param name="path">已存在的文件或目录。</param>
        /// <returns>文件系统属性包含 ReparsePoint 时返回 true。</returns>
        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        /// <summary>
        /// 确认平台目录的实际发布载荷与 manifest 文件集合完全一致。
        /// </summary>
        /// <param name="platformRoot">平台根。</param>
        /// <param name="manifestFiles">manifest 已验证文件集合。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>物理载荷集合完全一致时返回 true。</returns>
        private static bool TryValidateActualFileSet(
            string platformRoot,
            ISet<string> manifestFiles,
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
        /// 不跟随目录链接遍历平台目录，并收集实际发布载荷。
        /// </summary>
        /// <param name="platformRoot">平台根。</param>
        /// <param name="files">实际载荷完整路径集合。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>遍历期间未发现链接项时返回 true。</returns>
        private static bool TryCollectActualFiles(
            string platformRoot,
            out HashSet<string> files,
            out string error)
        {
            files = new HashSet<string>(sRuntimePathComparer);
            error = string.Empty;
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(platformRoot);
            while (pendingDirectories.Count > 0)
            {
                var current = pendingDirectories.Pop();
                if (!TryCollectDirectoryEntries(current, platformRoot, pendingDirectories, files, out error))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 收集单层目录的子目录和文件，链接项直接终止校验。
        /// </summary>
        /// <param name="current">当前目录。</param>
        /// <param name="platformRoot">平台根。</param>
        /// <param name="pendingDirectories">待遍历目录栈。</param>
        /// <param name="files">实际发布载荷集合。</param>
        /// <param name="error">验证失败原因。</param>
        /// <returns>当前目录不存在链接载荷时返回 true。</returns>
        private static bool TryCollectDirectoryEntries(
            string current,
            string platformRoot,
            Stack<string> pendingDirectories,
            ISet<string> files,
            out string error)
        {
            error = string.Empty;
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (IsReparsePoint(directory))
                {
                    error = "Runtime profile contains a symbolic link or reparse-point directory.";
                    return false;
                }

                if (!IsRuntimeStateDirectory(directory))
                {
                    pendingDirectories.Push(directory);
                }
            }

            foreach (var path in Directory.EnumerateFiles(current))
            {
                if (IsReparsePoint(path))
                {
                    error = "Runtime profile contains a symbolic link or reparse-point file.";
                    return false;
                }

                if (IsRuntimePayloadFile(platformRoot, path))
                {
                    files.Add(Path.GetFullPath(path));
                }
            }

            return true;
        }

        /// <summary>
        /// 验证入口位于 Runtime 根内、物理存在且已通过文件完整性校验。
        /// </summary>
        /// <param name="runtimeRoot">Runtime 根。</param>
        /// <param name="entry">manifest 相对入口。</param>
        /// <param name="files">可信文件集合。</param>
        /// <param name="fullPath">可信入口完整路径。</param>
        /// <returns>入口属于文件集合时返回 true。</returns>
        private static bool TryResolveListedEntry(
            string runtimeRoot,
            string entry,
            ISet<string> files,
            out string fullPath)
        {
            return TryResolveFileInside(runtimeRoot, entry, out fullPath) && files.Contains(fullPath);
        }

        /// <summary>
        /// 判断文件是否为 manifest 管理的发布载荷。
        /// </summary>
        /// <param name="platformRoot">平台根。</param>
        /// <param name="path">候选文件。</param>
        /// <returns>非 PDB 且不位于平台内 `.yokiframe` 目录时返回 true。</returns>
        private static bool IsRuntimePayloadFile(string platformRoot, string path)
        {
            return !string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase)
                && !ContainsRelativeDirectory(platformRoot, path, RUNTIME_STATE_DIRECTORY_NAME);
        }

        /// <summary>
        /// 判断目录是否为平台内允许忽略的运行态状态目录。
        /// </summary>
        /// <param name="path">候选目录。</param>
        /// <returns>目录名为 `.yokiframe` 时返回 true。</returns>
        private static bool IsRuntimeStateDirectory(string path)
        {
            return string.Equals(
                Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                RUNTIME_STATE_DIRECTORY_NAME,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断文件相对平台根的祖先目录是否包含指定名称。
        /// </summary>
        /// <param name="platformRoot">平台根。</param>
        /// <param name="path">平台内文件。</param>
        /// <param name="directoryName">待匹配目录名。</param>
        /// <returns>相对祖先目录匹配时返回 true。</returns>
        private static bool ContainsRelativeDirectory(string platformRoot, string path, string directoryName)
        {
            var relativePath = Path.GetRelativePath(platformRoot, path).Replace('\\', '/');
            var lastSeparator = relativePath.LastIndexOf('/');
            if (lastSeparator < 0)
            {
                return false;
            }

            var segments = relativePath.Substring(0, lastSeparator)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                if (string.Equals(segments[index], directoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 识别当前宿主及其它支持平台的绝对路径写法。
        /// </summary>
        /// <param name="path">待检查路径。</param>
        /// <returns>具有根目录、UNC 或盘符语义时返回 true。</returns>
        private static bool IsPortableRooted(string path)
        {
            var hasDrivePrefix = path.Length >= 2
                && ((path[0] >= 'a' && path[0] <= 'z') || (path[0] >= 'A' && path[0] <= 'Z'))
                && path[1] == ':';
            return Path.IsPathRooted(path)
                || path.StartsWith("/", StringComparison.Ordinal)
                || path.StartsWith("\\", StringComparison.Ordinal)
                || hasDrivePrefix;
        }

        /// <summary>
        /// 将两类 manifest 路径分隔符转换为当前宿主分隔符。
        /// </summary>
        /// <param name="path">跨平台相对路径。</param>
        /// <returns>当前宿主可解析路径。</returns>
        private static string NormalizeForHost(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// 获取当前文件系统使用的路径大小写比较规则。
        /// </summary>
        /// <returns>Windows 忽略大小写，其它宿主区分大小写。</returns>
        private static StringComparison RuntimePathComparison()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}
#endif
