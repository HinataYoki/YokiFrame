using System;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// YokiFrame 1.0 兼容日志入口。新代码优先使用 LogKit，这里只负责把旧 KitLogger 调用转发到统一门面。
    /// </summary>
    public static class KitLogger
    {
        /// <summary>
        /// 旧版 KitLogger 日志过滤等级。
        /// </summary>
        public enum LogLevel
        {
            /// <summary>
            /// 禁用所有日志。
            /// </summary>
            None,

            /// <summary>
            /// 仅写入错误日志。
            /// </summary>
            Error,

            /// <summary>
            /// 写入警告和错误日志。
            /// </summary>
            Warning,

            /// <summary>
            /// 写入所有日志。
            /// </summary>
            All
        }

        /// <summary>
        /// 旧版 KitLogger 当前日志过滤等级。
        /// </summary>
        public static LogLevel Level = LogLevel.All;

        /// <summary>
        /// 写入普通信息级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Log(object message)
        {
            if (ShouldWrite(YokiFrame.LogLevel.Info))
                LogKit.Log(message);
        }

        /// <summary>
        /// 写入警告级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Warning(object message)
        {
            if (ShouldWrite(YokiFrame.LogLevel.Warning))
                LogKit.Warning(message);
        }

        /// <summary>
        /// 写入错误级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Error(object message)
        {
            if (ShouldWrite(YokiFrame.LogLevel.Error))
                LogKit.Error(message);
        }

        /// <summary>
        /// 写入异常日志。
        /// </summary>
        /// <param name="exception">要记录的异常。</param>
        public static void Exception(Exception exception)
        {
            if (ShouldWrite(YokiFrame.LogLevel.Error))
                LogKit.Exception(exception);
        }

        /// <summary>
        /// 将旧版 KitLogger 和 LogKit 运行时设置恢复为默认值。
        /// </summary>
        public static void ResetToDefault()
        {
            Level = LogLevel.All;
            LogKitSettings.ResetToDefaults();
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入普通信息日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugLog(string message)
        {
            Log(message);
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入警告日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugWarning(string message)
        {
            Warning(message);
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入错误日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugError(string message)
        {
            Error(message);
        }

        private static bool ShouldWrite(YokiFrame.LogLevel level)
        {
            if (Level == LogLevel.None)
                return false;
            if (Level == LogLevel.Error)
                return level == YokiFrame.LogLevel.Error;
            if (Level == LogLevel.Warning)
                return level == YokiFrame.LogLevel.Warning || level == YokiFrame.LogLevel.Error;

            return true;
        }
    }
}
