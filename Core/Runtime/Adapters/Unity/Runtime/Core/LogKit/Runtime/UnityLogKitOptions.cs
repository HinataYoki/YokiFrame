#if !GODOT
using System;
using System.IO;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity LogKit 运行时配置。Unity 专属的文件、IMGUI 和持久目录配置留在 Adapter 层。
    /// </summary>
    [Serializable]
    public sealed class UnityLogKitOptions
    {
        private const int DEFAULT_MAX_FILE_SIZE_MB = 100;
        private const string DEFAULT_EDITOR_FILE_NAME = LogKitSettings.DEFAULT_EDITOR_FILE_NAME;
        private const string DEFAULT_PLAYER_FILE_NAME = LogKitSettings.DEFAULT_PLAYER_FILE_NAME;

        /// <summary>
        /// 编辑器下是否保存日志到文件。
        /// </summary>
        [Tooltip("编辑器下是否保存日志到文件")]
        public bool SaveLogInEditor;

        /// <summary>
        /// Player 下是否保存日志到文件。
        /// </summary>
        [Tooltip("Player 下是否保存日志到文件")]
        public bool SaveLogInPlayer = true;

        /// <summary>
        /// Player 下是否启用 IMGUI 日志面板。
        /// </summary>
        [Tooltip("Player 下是否启用 IMGUI 日志面板")]
        public bool EnableIMGUIInPlayer;

        /// <summary>
        /// 是否启用日志文件行级 AES 加密。
        /// </summary>
        [Tooltip("是否启用日志文件行级 AES 加密")]
        public bool EnableEncryption = true;

        /// <summary>
        /// 文件写入队列最大容量。
        /// </summary>
        [Tooltip("文件写入队列最大容量")]
        public int MaxQueueSize = 20000;

        /// <summary>
        /// 同一条日志允许连续重复的次数，超过后丢弃。
        /// </summary>
        [Tooltip("同一条日志允许连续重复的次数，超过后丢弃")]
        public int MaxSameLogCount = 50;

        /// <summary>
        /// 日志文件最大保留天数。
        /// </summary>
        [Tooltip("日志文件最大保留天数")]
        public int MaxRetentionDays = 15;

        /// <summary>
        /// 单个日志文件最大大小，单位 MB。
        /// </summary>
        [Tooltip("单个日志文件最大大小，单位 MB")]
        public int MaxFileSizeMB = DEFAULT_MAX_FILE_SIZE_MB;

        /// <summary>
        /// IMGUI 日志面板最大显示条数。
        /// </summary>
        [Tooltip("IMGUI 日志面板最大显示条数")]
        public int IMGUIMaxLogCount = 200;

        /// <summary>
        /// 日志目录，留空时使用 Application.persistentDataPath/LogFiles。
        /// </summary>
        [Tooltip("日志目录。留空时使用 Application.persistentDataPath/LogFiles")]
        public string LogDirectory = string.Empty;

        /// <summary>
        /// 编辑器日志文件名。
        /// </summary>
        [Tooltip("编辑器日志文件名")]
        public string EditorFileName = DEFAULT_EDITOR_FILE_NAME;

        /// <summary>
        /// Player 日志文件名。
        /// </summary>
        [Tooltip("Player 日志文件名")]
        public string PlayerFileName = DEFAULT_PLAYER_FILE_NAME;

        /// <summary>
        /// 单个日志文件最大大小，单位 bytes。
        /// </summary>
        public long MaxFileBytes
        {
            get { return Math.Max(1, MaxFileSizeMB) * 1024L * 1024L; }
            set
            {
                if (value <= 0)
                {
                    MaxFileSizeMB = DEFAULT_MAX_FILE_SIZE_MB;
                    return;
                }

                var mb = value / (1024L * 1024L);
                if (mb <= 0)
                    mb = 1;
                if (mb > int.MaxValue)
                    mb = int.MaxValue;
                MaxFileSizeMB = (int)mb;
            }
        }

        /// <summary>
        /// 创建默认 Unity LogKit 配置。
        /// </summary>
        /// <returns>默认配置实例。</returns>
        public static UnityLogKitOptions CreateDefault()
        {
            return new();
        }

        /// <summary>
        /// 克隆当前配置。
        /// </summary>
        /// <returns>当前配置的副本。</returns>
        public UnityLogKitOptions Clone()
        {
            return new()
            {
                SaveLogInEditor = SaveLogInEditor,
                SaveLogInPlayer = SaveLogInPlayer,
                EnableIMGUIInPlayer = EnableIMGUIInPlayer,
                EnableEncryption = EnableEncryption,
                MaxQueueSize = MaxQueueSize,
                MaxSameLogCount = MaxSameLogCount,
                MaxRetentionDays = MaxRetentionDays,
                MaxFileSizeMB = MaxFileSizeMB,
                IMGUIMaxLogCount = IMGUIMaxLogCount,
                LogDirectory = LogDirectory,
                EditorFileName = EditorFileName,
                PlayerFileName = PlayerFileName
            };
        }

        /// <summary>
        /// 修正配置中的非法值。
        /// </summary>
        public void Normalize()
        {
            if (MaxQueueSize <= 0)
                MaxQueueSize = 1;
            if (MaxSameLogCount < 0)
                MaxSameLogCount = 0;
            if (MaxRetentionDays <= 0)
                MaxRetentionDays = 1;
            if (MaxFileSizeMB <= 0)
                MaxFileSizeMB = DEFAULT_MAX_FILE_SIZE_MB;
            if (IMGUIMaxLogCount <= 0)
                IMGUIMaxLogCount = 200;
            if (string.IsNullOrEmpty(EditorFileName))
                EditorFileName = DEFAULT_EDITOR_FILE_NAME;
            if (string.IsNullOrEmpty(PlayerFileName))
                PlayerFileName = DEFAULT_PLAYER_FILE_NAME;
        }

        /// <summary>
        /// 判断当前运行环境是否应该保存日志文件。
        /// </summary>
        /// <returns>当前环境应保存日志文件时返回 true。</returns>
        public bool ShouldSaveForCurrentRuntime()
        {
            return Application.isEditor ? SaveLogInEditor : SaveLogInPlayer;
        }

        /// <summary>
        /// 解析当前日志目录。
        /// </summary>
        /// <returns>日志目录路径。</returns>
        public string ResolveLogDirectory()
        {
            if (!string.IsNullOrEmpty(LogDirectory))
                return LogDirectory;

            return Path.Combine(Application.persistentDataPath, "LogFiles");
        }

        /// <summary>
        /// 解析当前日志文件路径。
        /// </summary>
        /// <returns>日志文件路径。</returns>
        public string ResolveLogFilePath()
        {
            var fileName = Application.isEditor ? EditorFileName : PlayerFileName;
            if (string.IsNullOrEmpty(fileName))
                fileName = Application.isEditor ? DEFAULT_EDITOR_FILE_NAME : DEFAULT_PLAYER_FILE_NAME;

            return Path.Combine(ResolveLogDirectory(), fileName);
        }
    }
}
#endif
