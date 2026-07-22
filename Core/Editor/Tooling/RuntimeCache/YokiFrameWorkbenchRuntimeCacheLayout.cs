using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 统一计算项目级 Workbench Runtime 缓存路径，确保包根始终保持只读源码状态。
    /// </summary>
    public static class YokiFrameWorkbenchRuntimeCacheLayout
    {
        /// <summary>项目级 YokiFrame 状态目录名称。</summary>
        public const string YOKIFRAME_DIRECTORY_NAME = ".yokiframe";

        /// <summary>可再生 Runtime 缓存目录名称。</summary>
        public const string RUNTIME_DIRECTORY_NAME = "runtime";

        /// <summary>YokiFrame 包身份目录名称。</summary>
        public const string PACKAGE_DIRECTORY_NAME = "com.hinatayoki.yokiframe";

        /// <summary>指向当前有效 Runtime 的项目级索引文件名。</summary>
        public const string CURRENT_FILE_NAME = "current.json";

        /// <summary>
        /// 获取项目内单一 YokiFrame Runtime 缓存容器。
        /// </summary>
        /// <param name="projectRoot">Unity 或 Godot 项目根目录。</param>
        /// <returns>项目级 Runtime 缓存容器完整路径。</returns>
        public static string GetCacheRoot(string projectRoot)
        {
            return Path.Combine(RequireProjectRoot(projectRoot), YOKIFRAME_DIRECTORY_NAME, RUNTIME_DIRECTORY_NAME, PACKAGE_DIRECTORY_NAME);
        }

        /// <summary>
        /// 获取指定源码指纹对应的不可变 Runtime 目录。
        /// </summary>
        /// <param name="projectRoot">Unity 或 Godot 项目根目录。</param>
        /// <param name="sourceFingerprint">实际 Workbench 构建输入的 SHA-256 指纹。</param>
        /// <returns>指纹 Runtime 根目录完整路径。</returns>
        public static string GetRuntimeRoot(string projectRoot, string sourceFingerprint)
        {
            return Path.Combine(GetCacheRoot(projectRoot), RequireFingerprint(sourceFingerprint));
        }

        /// <summary>
        /// 获取项目级当前 Runtime 指针文件完整路径。
        /// </summary>
        /// <param name="projectRoot">Unity 或 Godot 项目根目录。</param>
        /// <returns>`current.json` 完整路径。</returns>
        public static string GetCurrentFilePath(string projectRoot)
        {
            return Path.Combine(GetCacheRoot(projectRoot), CURRENT_FILE_NAME);
        }

        /// <summary>
        /// 验证项目根文本并返回规范化完整路径；目录可在调用方首次写入时创建。
        /// </summary>
        /// <param name="projectRoot">待验证的项目根路径。</param>
        /// <returns>规范化完整路径。</returns>
        private static string RequireProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            return Path.GetFullPath(projectRoot);
        }

        /// <summary>
        /// 验证 SHA-256 指纹只包含 64 个小写十六进制字符，避免缓存目录路径逃逸。
        /// </summary>
        /// <param name="sourceFingerprint">待验证的源码指纹。</param>
        /// <returns>已验证的指纹文本。</returns>
        private static string RequireFingerprint(string sourceFingerprint)
        {
            if (string.IsNullOrWhiteSpace(sourceFingerprint) || sourceFingerprint.Length != 64)
            {
                throw new ArgumentException("Source fingerprint must be a SHA-256 hexadecimal string.", nameof(sourceFingerprint));
            }

            foreach (var character in sourceFingerprint)
            {
                var isDigit = character >= '0' && character <= '9';
                var isLowerHex = character >= 'a' && character <= 'f';
                if (!isDigit && !isLowerHex)
                {
                    throw new ArgumentException("Source fingerprint must be a SHA-256 hexadecimal string.", nameof(sourceFingerprint));
                }
            }

            return sourceFingerprint;
        }
    }
}
