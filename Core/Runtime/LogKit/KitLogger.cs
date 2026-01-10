using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 日志系统主入口
    /// </summary>
    public static partial class KitLogger
    {
        #region 配置项

        public enum LogLevel { None, Error, Warning, All }

        public static LogLevel Level = LogLevel.All;

        /// <summary>
        /// 获取 LogKit 配置（内部使用）
        /// </summary>
        private static YokiFrameSettings.LogKitSettings Settings => YokiFrameSettings.Instance.LogKit;

        // 运行时配置缓存（从 Settings 初始化）
        private static bool sEnableEncryption;
        private static int sMaxQueueSize;
        private static int sMaxSameLogCount;
        private static int sMaxRetentionDays;
        private static long sMaxFileBytes;

        public static bool EnableEncryption
        {
            get { EnsureInitialized(); return sEnableEncryption; }
            set { sEnableEncryption = value; UpdateSettings(); }
        }

        public static int MaxQueueSize
        {
            get { EnsureInitialized(); return sMaxQueueSize; }
            set { sMaxQueueSize = value; UpdateSettings(); }
        }

        public static int MaxSameLogCount
        {
            get { EnsureInitialized(); return sMaxSameLogCount; }
            set { sMaxSameLogCount = value; UpdateSettings(); }
        }

        public static int MaxRetentionDays
        {
            get { EnsureInitialized(); return sMaxRetentionDays; }
            set { sMaxRetentionDays = value; UpdateSettings(); }
        }

        public static long MaxFileBytes
        {
            get { EnsureInitialized(); return sMaxFileBytes; }
            set { sMaxFileBytes = value; UpdateSettings(); }
        }

        /// <summary>
        /// 确保设置已加载（编辑器和运行时通用）
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!sInitialized) LoadSettings();
        }

        /// <summary>
        /// 重置所有配置为默认值
        /// </summary>
        public static void ResetToDefault()
        {
            Settings.ResetToDefault();
#if UNITY_EDITOR
            YokiFrameSettings.Instance.Save();
#endif
            sInitialized = false;
            LoadSettings();
        }

        private static bool sSaveLogInEditor;
        private static bool sSaveLogInPlayer;
        private static bool sEnableIMGUIInPlayer;
        private static bool sInitialized;

        /// <summary>
        /// 编辑器下是否保存日志到文件
        /// </summary>
        public static bool SaveLogInEditor
        {
            get { EnsureInitialized(); return sSaveLogInEditor; }
            set
            {
                if (sSaveLogInEditor != value)
                {
                    sSaveLogInEditor = value;
                    if (Application.isEditor)
                    {
                        if (sSaveLogInEditor) KitLoggerWriter.Initialize();
                        else KitLoggerWriter.Shutdown();
                    }
                    UpdateSettings();
                }
            }
        }

        /// <summary>
        /// 真机下是否保存日志到文件
        /// </summary>
        public static bool SaveLogInPlayer
        {
            get { EnsureInitialized(); return sSaveLogInPlayer; }
            set
            {
                if (sSaveLogInPlayer != value)
                {
                    sSaveLogInPlayer = value;
                    if (!Application.isEditor)
                    {
                        if (sSaveLogInPlayer) KitLoggerWriter.Initialize();
                        else KitLoggerWriter.Shutdown();
                    }
                    UpdateSettings();
                }
            }
        }

        /// <summary>
        /// 真机下是否启用 IMGUI 日志显示
        /// </summary>
        public static bool EnableIMGUIInPlayer
        {
            get { EnsureInitialized(); return sEnableIMGUIInPlayer; }
            set
            {
                if (sEnableIMGUIInPlayer != value)
                {
                    sEnableIMGUIInPlayer = value;
                    if (!Application.isEditor)
                    {
                        if (sEnableIMGUIInPlayer) EnableIMGUI();
                        else DisableIMGUI();
                    }
                    UpdateSettings();
                }
            }
        }

        /// <summary>
        /// 从配置文件加载设置
        /// </summary>
        private static void LoadSettings()
        {
            if (sInitialized) return;
            sInitialized = true;

            var settings = Settings;
            if (settings == null) return;

            sSaveLogInEditor = settings.SaveLogInEditor;
            sSaveLogInPlayer = settings.SaveLogInPlayer;
            sEnableIMGUIInPlayer = settings.EnableIMGUIInPlayer;
            sEnableEncryption = settings.EnableEncryption;
            sMaxQueueSize = settings.MaxQueueSize;
            sMaxSameLogCount = settings.MaxSameLogCount;
            sMaxRetentionDays = settings.MaxRetentionDays;
            sMaxFileBytes = settings.MaxFileBytes;
        }

        /// <summary>
        /// 保存设置到配置文件（仅编辑器）
        /// </summary>
        private static void UpdateSettings()
        {
#if UNITY_EDITOR
            var settings = Settings;
            if (settings == null) return;

            settings.SaveLogInEditor = sSaveLogInEditor;
            settings.SaveLogInPlayer = sSaveLogInPlayer;
            settings.EnableIMGUIInPlayer = sEnableIMGUIInPlayer;
            settings.EnableEncryption = sEnableEncryption;
            settings.MaxQueueSize = sMaxQueueSize;
            settings.MaxSameLogCount = sMaxSameLogCount;
            settings.MaxRetentionDays = sMaxRetentionDays;
            settings.MaxFileSizeMB = (int)(sMaxFileBytes / 1024 / 1024);
            YokiFrameSettings.Instance.Save();
#endif
        }

        #endregion

        #region 公开接口

        [HideInCallstack]
        public static void Log(object msg, Object context = null) => LogInternal(LogType.Log, msg, context);

        [HideInCallstack]
        public static void Warning(object msg, Object context = null) => LogInternal(LogType.Warning, msg, context);

        [HideInCallstack]
        public static void Error(object msg, Object context = null) => LogInternal(LogType.Error, msg, context);

        [HideInCallstack]
        public static void Exception(Exception ex, Object context = null) => LogInternal(LogType.Exception, ex, context);

        [HideInCallstack]
        private static void LogInternal(LogType type, object msg, Object context)
        {
            if (Level == LogLevel.None) return;
            if (Level == LogLevel.Error && type != LogType.Error && type != LogType.Exception) return;
            if (Level == LogLevel.Warning && type == LogType.Log) return;

            switch (type)
            {
                case LogType.Log: Debug.Log(msg, context); break;
                case LogType.Warning: Debug.LogWarning(msg, context); break;
                case LogType.Error: Debug.LogError(msg, context); break;
                case LogType.Assert: Debug.LogAssertion(msg, context); break;
                case LogType.Exception:
                    if (msg is Exception e) Debug.LogException(e, context);
                    else Debug.LogError(msg, context);
                    break;
            }
        }

        #endregion

        #region 系统初始化

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            // 从配置文件加载设置
            LoadSettings();

            // 根据配置决定是否启用功能
            if (Application.isEditor)
            {
                if (sSaveLogInEditor) KitLoggerWriter.Initialize();
            }
            else
            {
                if (sSaveLogInPlayer) KitLoggerWriter.Initialize();
                if (sEnableIMGUIInPlayer) EnableIMGUI();
            }
        }

        #endregion

        #region IMGUI 日志显示

        /// <summary>
        /// 启用 IMGUI 日志显示（用于打包后调试）
        /// </summary>
        public static KitLoggerIMGUI EnableIMGUI(int maxLogCount = 200) => KitLoggerIMGUI.Enable(maxLogCount);

        /// <summary>
        /// 禁用 IMGUI 日志显示
        /// </summary>
        public static void DisableIMGUI() => KitLoggerIMGUI.Disable();

        #endregion
    }
}
