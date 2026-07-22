#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 为 Shared Memory Telemetry 生成跨进程一致的项目作用域，避免同机多个项目串写全局 Named Map。
    /// </summary>
    public static class YokiFrameSharedMemoryTelemetryProjectScopeId
    {
        private const ulong FNV1A_64_OFFSET_BASIS = 14695981039346656037UL;
        private const ulong FNV1A_64_PRIME = 1099511628211UL;

        /// <summary>
        /// 按当前平台的路径大小写与分隔符规则规范化项目根，并对完整 UTF-8 字节生成十六位 FNV-1a 64 作用域。
        /// </summary>
        /// <param name="projectRoot">当前宿主项目绝对根目录。</param>
        /// <returns>可安全进入 Named Map 名称的十六位项目作用域。</returns>
        public static string Compute(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Telemetry project root is required.", nameof(projectRoot));
            }

            unchecked
            {
                var hash = FNV1A_64_OFFSET_BASIS;
                var normalizedPath = NormalizeProjectRoot(projectRoot);
                var pathBytes = Encoding.UTF8.GetBytes(normalizedPath);
                for (var index = 0; index < pathBytes.Length; index++)
                {
                    hash ^= pathBytes[index];
                    hash *= FNV1A_64_PRIME;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// 生成只用于跨进程身份计算的规范路径；Windows 忽略大小写并统一分隔符，POSIX 保留大小写与反斜杠字符。
        /// </summary>
        /// <param name="projectRoot">当前宿主提供的绝对项目根。</param>
        /// <returns>去掉非根目录末尾分隔符后的平台语义路径。</returns>
        private static string NormalizeProjectRoot(string projectRoot)
        {
            var length = GetNormalizedLength(projectRoot);
            var normalizedCharacters = new char[length];
            var isWindows = Path.DirectorySeparatorChar == '\\';
            for (var index = 0; index < length; index++)
            {
                var character = projectRoot[index];
                if (IsDirectorySeparator(character))
                {
                    character = '/';
                }

                normalizedCharacters[index] = isWindows
                    ? char.ToLowerInvariant(character)
                    : character;
            }

            return new string(normalizedCharacters);
        }

        /// <summary>
        /// 去掉项目根末尾多余分隔符，但保留盘符根目录本身。
        /// </summary>
        /// <param name="projectRoot">原始项目根。</param>
        /// <returns>参与 hash 的字符数量。</returns>
        private static int GetNormalizedLength(string projectRoot)
        {
            var length = projectRoot.Length;
            var rootLength = Path.GetPathRoot(projectRoot)?.Length ?? 0;
            while (length > rootLength && IsDirectorySeparator(projectRoot[length - 1]))
            {
                length--;
            }

            return length;
        }

        /// <summary>
        /// 判断字符是否为当前平台认可的主目录分隔符或备用目录分隔符。
        /// </summary>
        /// <param name="character">待检查字符。</param>
        /// <returns>属于目录分隔符时返回 true。</returns>
        private static bool IsDirectorySeparator(char character)
        {
            return character == Path.DirectorySeparatorChar
                || character == Path.AltDirectorySeparatorChar;
        }
    }
}
#endif
