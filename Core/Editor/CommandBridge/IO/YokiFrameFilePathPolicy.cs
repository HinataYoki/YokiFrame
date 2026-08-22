#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace YokiFrame
{
    /// <summary>
    /// FileBridge 路径防护的唯一机制实现：受控根逃逸检查与现存路径链重解析点（符号链接/Junction）扫描。
    /// Unity Editor、Godot Runtime/Editor 宿主与工具链 Client 经源码链接复用本类型，
    /// 禁止在调用方再复制私有扫描实现；失败统一抛出 <see cref="IOException"/>，
    /// 消息携带稳定短语供各调用方转换为自身错误模型（协议错误码、宿主诊断等）。
    /// </summary>
    internal static class YokiFrameFilePathPolicy
    {
        /// <summary>逃逸失败的稳定消息标记，供调用方按需映射错误码。</summary>
        public const string ESCAPED_ROOT_MARKER = "escaped the project root";

        /// <summary>重解析点失败的稳定消息标记；宿主测试依赖该短语。</summary>
        public const string REPARSE_POINT_MARKER = "symbolic link or junction";

        /// <summary>
        /// 合并路径片段并确认结果位于指定根目录内，且现存路径链不含重解析点。
        /// </summary>
        /// <param name="rootPath">允许访问的受控根目录。</param>
        /// <param name="segments">待合并的路径片段。</param>
        /// <returns>已归一化的完整路径。</returns>
        public static string CombineInside(string rootPath, params string[] segments)
        {
            var combinedPath = rootPath;
            foreach (var segment in segments)
            {
                combinedPath = Path.Combine(combinedPath, segment);
            }

            var fullPath = EnsureInside(rootPath, combinedPath);
            EnsureNoReparsePoint(rootPath, fullPath);
            return fullPath;
        }

        /// <summary>
        /// 确认候选路径位于根目录内；失败时抛出带逃逸标记的 IOException。
        /// </summary>
        /// <param name="rootPath">允许访问的根目录。</param>
        /// <param name="candidatePath">待检查的候选路径。</param>
        /// <returns>已归一化的候选路径。</returns>
        public static string EnsureInside(string rootPath, string candidatePath)
        {
            var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
            var fullCandidate = Path.GetFullPath(candidatePath);
            // Unity 宿主目标不含 OperatingSystem.IsWindows()，统一使用 RuntimeInformation 判定平台大小写语义。
            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (fullCandidate.StartsWith(fullRoot, comparison)
                || string.Equals(RemoveTrailingSeparator(fullCandidate), RemoveTrailingSeparator(fullRoot), comparison))
            {
                return fullCandidate;
            }

            throw new IOException("FileBridge path " + ESCAPED_ROOT_MARKER + ": " + fullCandidate);
        }

        /// <summary>
        /// 拒绝受控根及其到目标的现存路径链包含符号链接、Junction 或其它重解析点。
        /// </summary>
        /// <param name="rootPath">受控根目录。</param>
        /// <param name="candidatePath">已位于根内的候选路径。</param>
        public static void EnsureNoReparsePoint(string rootPath, string candidatePath)
        {
            var fullRoot = Path.GetFullPath(rootPath);
            var fullCandidate = EnsureInside(fullRoot, candidatePath);
            EnsurePathComponentIsNotReparsePoint(fullRoot);
            EnsureNoReparsePointBelow(fullRoot, fullCandidate);
        }

        /// <summary>
        /// 只校验已验证根之下到目标的现存组件不是重解析点；根自身由调用方先行校验。
        /// </summary>
        /// <param name="verifiedRoot">同轮已完成全链校验的固定根。</param>
        /// <param name="candidatePath">待检查的候选路径。</param>
        public static void EnsureNoReparsePointBelow(string verifiedRoot, string candidatePath)
        {
            var current = verifiedRoot;
            var relativePath = Path.GetRelativePath(verifiedRoot, candidatePath);
            foreach (var segment in relativePath.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                EnsurePathComponentIsNotReparsePoint(current);
            }
        }

        /// <summary>
        /// 校验单个现存文件系统组件不是重解析点；组件不存在时视为通过。
        /// </summary>
        /// <param name="path">待检查的路径组件。</param>
        public static void EnsurePathComponentIsNotReparsePoint(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path))
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("FileBridge path contains a " + REPARSE_POINT_MARKER + ": " + path);
            }
        }

        /// <summary>给目录路径补齐结尾分隔符，避免 sibling prefix 绕过 containment 检查。</summary>
        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        /// <summary>移除路径结尾分隔符，用于根目录自身的等值判断。</summary>
        private static string RemoveTrailingSeparator(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
#endif
