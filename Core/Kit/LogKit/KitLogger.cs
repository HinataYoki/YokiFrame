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
        public static bool EnableEncryption = true;
        public static bool AutoEnableWriteLogToFile = false;

        /// <summary>限制队列最大数量，防止无限报错导致内存撑爆</summary>
        public static int MaxQueueSize = 20000;

        /// <summary>连续相同日志的忽略阈值，防止 Update 报错把磁盘写死</summary>
        public static int MaxSameLogCount = 50;

        public static int MaxRetentionDays = 10;
        public static long MaxFileBytes = 50 * 1024 * 1024;

        private static bool sSaveLogInEditor;

        public static bool SaveLogInEditor
        {
            get => sSaveLogInEditor;
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
                }
            }
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (AutoEnableWriteLogToFile)
            {
                KitLoggerWriter.Initialize();
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
