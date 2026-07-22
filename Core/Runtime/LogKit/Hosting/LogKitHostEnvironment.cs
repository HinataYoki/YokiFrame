#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.IO;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 保存当前宿主为 LogKit 解析出的文件位置和真实能力；Core 快照只读取该纯 C# 状态。
    /// </summary>
    public static class LogKitHostEnvironment
    {
        private static readonly object sLock = new object();
        private static string sDefaultDirectory = string.Empty;
        private static string sDirectory = string.Empty;
        private static string sEditorPath = string.Empty;
        private static string sPlayerPath = string.Empty;
        private static bool sConfigured;
        private static bool sSettingsApply;
        private static bool sFilePreview;
        private static bool sFileWriter;
        private static bool sPlayerImGui;
        private static bool sEncryption;
        private static bool sPathsResolved;
        private static long sVersion;

        /// <summary>获取宿主文件位置或能力发生变化时递增的单调版本。</summary>
        public static long Version => Interlocked.Read(ref sVersion);

        /// <summary>
        /// 配置当前宿主的默认日志目录与实际能力；文件路径延后到首次工具读取时按当前 Settings 解析。
        /// </summary>
        /// <param name="defaultDirectory">当前宿主的默认日志目录绝对路径。</param>
        /// <param name="settingsApply">当前进程是否允许应用 LogKit 设置。</param>
        /// <param name="filePreview">当前进程是否允许有界读取日志文件。</param>
        /// <param name="fileWriter">当前版本是否真正启用了文件写入器。</param>
        /// <param name="playerImGui">当前版本是否真正启用了 Player IMGUI。</param>
        /// <param name="encryption">当前版本是否真正启用了可信日志加密。</param>
        public static void Configure(
            string defaultDirectory,
            bool settingsApply,
            bool filePreview,
            bool fileWriter,
            bool playerImGui,
            bool encryption)
        {
            string normalizedDirectory = NormalizeRequiredDirectory(defaultDirectory);
            lock (sLock)
            {
                sDefaultDirectory = normalizedDirectory;
                sConfigured = true;
                sSettingsApply = settingsApply;
                sFilePreview = filePreview;
                sFileWriter = fileWriter;
                sPlayerImGui = playerImGui;
                sEncryption = encryption;
                sDirectory = string.Empty;
                sEditorPath = string.Empty;
                sPlayerPath = string.Empty;
                sPathsResolved = false;
                BumpVersion();
            }
        }

        /// <summary>
        /// 清除当前宿主环境；宿主关闭后 Provider 会明确报告能力不可用。
        /// </summary>
        public static void Reset()
        {
            lock (sLock)
            {
                sDefaultDirectory = string.Empty;
                sDirectory = string.Empty;
                sEditorPath = string.Empty;
                sPlayerPath = string.Empty;
                sConfigured = false;
                sSettingsApply = false;
                sFilePreview = false;
                sFileWriter = false;
                sPlayerImGui = false;
                sEncryption = false;
                sPathsResolved = false;
                BumpVersion();
            }
        }

        /// <summary>
        /// 在设置批量应用后重新解析目录和文件名；路径未变化时不制造额外版本。
        /// </summary>
        internal static void RefreshPathsFromSettings()
        {
            lock (sLock)
            {
                if (!sConfigured)
                {
                    return;
                }

                bool hadResolvedPaths = sPathsResolved;
                string oldDirectory = sDirectory;
                string oldEditorPath = sEditorPath;
                string oldPlayerPath = sPlayerPath;
                ResolvePathsLocked();
                sPathsResolved = true;
                if (!hadResolvedPaths
                    || !string.Equals(oldDirectory, sDirectory, StringComparison.Ordinal)
                    || !string.Equals(oldEditorPath, sEditorPath, StringComparison.Ordinal)
                    || !string.Equals(oldPlayerPath, sPlayerPath, StringComparison.Ordinal))
                {
                    BumpVersion();
                }
            }
        }

        /// <summary>
        /// 获取一次原子的宿主环境副本，避免 JSON 构建观察到混合路径和能力。
        /// </summary>
        /// <returns>当前宿主环境快照。</returns>
        internal static LogKitHostEnvironmentSnapshot Capture()
        {
            lock (sLock)
            {
                if (sConfigured)
                {
                    EnsurePathsResolvedLocked();
                }

                return new LogKitHostEnvironmentSnapshot
                {
                    Directory = sDirectory,
                    EditorPath = sEditorPath,
                    PlayerPath = sPlayerPath,
                    SettingsApply = sSettingsApply,
                    FilePreview = sFilePreview,
                    FileWriter = sFileWriter,
                    PlayerImGui = sPlayerImGui,
                    Encryption = sEncryption
                };
            }
        }

        /// <summary>
        /// 按稳定 kind 获取当前宿主解析后的日志文件路径。
        /// </summary>
        /// <param name="kind">只允许 editor 或 player。</param>
        /// <param name="path">成功时返回对应绝对路径。</param>
        /// <returns>宿主允许文件预览且 kind 有效时返回 true。</returns>
        internal static bool TryGetFilePath(string kind, out string path)
        {
            lock (sLock)
            {
                path = string.Empty;
                if (!sConfigured || !sFilePreview)
                {
                    return false;
                }

                EnsurePathsResolvedLocked();

                if (string.Equals(kind, "editor", StringComparison.Ordinal))
                {
                    path = sEditorPath;
                    return !string.IsNullOrEmpty(path);
                }

                if (string.Equals(kind, "player", StringComparison.Ordinal))
                {
                    path = sPlayerPath;
                    return !string.IsNullOrEmpty(path);
                }

                return false;
            }
        }

        /// <summary>首次工具读取时按当前设置解析目录以及 Editor/Player 文件路径。</summary>
        private static void EnsurePathsResolvedLocked()
        {
            if (sPathsResolved)
            {
                return;
            }

            ResolvePathsLocked();
            sPathsResolved = true;
        }

        /// <summary>按当前设置重新解析目录以及 Editor/Player 文件路径。</summary>
        private static void ResolvePathsLocked()
        {
            sDirectory = ResolveConfiguredDirectory(sDefaultDirectory);
            string editorName = ResolveFileName(
                LogKitSettings.EDITOR_FILE_NAME_KEY,
                LogKitSettings.DEFAULT_EDITOR_FILE_NAME);
            string playerName = ResolveFileName(
                LogKitSettings.PLAYER_FILE_NAME_KEY,
                LogKitSettings.DEFAULT_PLAYER_FILE_NAME);
            sEditorPath = Path.GetFullPath(Path.Combine(sDirectory, editorName));
            sPlayerPath = Path.GetFullPath(Path.Combine(sDirectory, playerName));
        }

        /// <summary>
        /// 解析用户目录覆盖；相对目录以宿主默认目录的父目录为根，避免依赖进程工作目录。
        /// </summary>
        /// <param name="defaultDirectory">宿主默认日志目录。</param>
        /// <returns>可用于文件状态和显式预览的绝对目录。</returns>
        private static string ResolveConfiguredDirectory(string defaultDirectory)
        {
            string configured = LogKitSettings.GetString(
                LogKitSettings.LOG_DIRECTORY_KEY,
                LogKitSettings.DEFAULT_LOG_DIRECTORY);
            if (string.IsNullOrWhiteSpace(configured))
            {
                return defaultDirectory;
            }

            try
            {
                if (Path.IsPathRooted(configured))
                {
                    return Path.GetFullPath(configured);
                }

                string parent = Path.GetDirectoryName(defaultDirectory) ?? defaultDirectory;
                return Path.GetFullPath(Path.Combine(parent, configured));
            }
            catch (Exception)
            {
                return defaultDirectory;
            }
        }

        /// <summary>
        /// 读取并校验单个日志文件名；损坏配置回落稳定默认值，绝不允许文件名携带目录。
        /// </summary>
        /// <param name="key">Runtime Settings key。</param>
        /// <param name="defaultValue">损坏或缺失时使用的文件名。</param>
        /// <returns>安全的单段文件名。</returns>
        private static string ResolveFileName(string key, string defaultValue)
        {
            string value = LogKitSettings.GetString(key, defaultValue);
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
                || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return defaultValue;
            }

            return value;
        }

        /// <summary>
        /// 校验宿主提供的默认目录并归一化为绝对路径。
        /// </summary>
        /// <param name="defaultDirectory">宿主默认目录。</param>
        /// <returns>归一化绝对路径。</returns>
        private static string NormalizeRequiredDirectory(string defaultDirectory)
        {
            if (string.IsNullOrWhiteSpace(defaultDirectory))
            {
                throw new ArgumentException("LogKit default directory is required.", nameof(defaultDirectory));
            }

            return Path.GetFullPath(defaultDirectory);
        }

        /// <summary>递增宿主环境版本，供版本化 Provider 即时刷新。</summary>
        private static void BumpVersion()
        {
            Interlocked.Increment(ref sVersion);
        }
    }

    /// <summary>表示供 LogKit SnapshotBuilder 原子读取的宿主环境副本。</summary>
    internal sealed class LogKitHostEnvironmentSnapshot
    {
        internal string Directory = string.Empty;
        internal string EditorPath = string.Empty;
        internal string PlayerPath = string.Empty;
        internal bool SettingsApply;
        internal bool FilePreview;
        internal bool FileWriter;
        internal bool PlayerImGui;
        internal bool Encryption;
    }
}
#endif
