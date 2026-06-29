using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关日志门面。宿主只需注入 IEngineLogger，Base 层不直接依赖 Unity/Godot 日志 API。
    /// </summary>
    public static class LogKit
    {
        private const int MAX_HISTORY = 128;

        private static readonly object sLock = new();
        private static readonly Queue<LogKitEntry> sHistory = new(MAX_HISTORY);
        private static IEngineLogger sLogger;
        private static bool sEnabled = true;
        private static LogLevel sMinimumLevel = LogLevel.Debug;
        private static int sDroppedCount;
        private static long sDiagnosticVersion;

        /// <summary>
        /// LogKit 诊断状态版本号；日志历史、过滤配置或后端变化时递增，用于快照失效判断。
        /// </summary>
        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }

        /// <summary>
        /// 获取或设置 LogKit 是否接收并转发日志。
        /// </summary>
        public static bool Enabled
        {
            get
            {
                lock (sLock)
                    return sEnabled;
            }
            set
            {
                lock (sLock)
                {
                    if (sEnabled == value)
                        return;

                    sEnabled = value;
                    BumpDiagnosticVersion();
                }
            }
        }

        /// <summary>
        /// 获取或设置当前最小日志等级，低于该等级的日志会被忽略。
        /// </summary>
        public static LogLevel MinimumLevel
        {
            get
            {
                lock (sLock)
                    return sMinimumLevel;
            }
            set
            {
                lock (sLock)
                {
                    var normalized = NormalizeLevel(value);
                    if (sMinimumLevel == normalized)
                        return;

                    sMinimumLevel = normalized;
                    BumpDiagnosticVersion();
                }
            }
        }

        /// <summary>
        /// 获取当前是否已经注入引擎日志后端。
        /// </summary>
        public static bool HasLogger
        {
            get
            {
                lock (sLock)
                    return sLogger != null;
            }
        }

        /// <summary>
        /// 获取当前日志后端类型名称；未注入时返回 None。
        /// </summary>
        public static string LoggerName
        {
            get
            {
                lock (sLock)
                    return sLogger != null ? sLogger.GetType().Name : "None";
            }
        }

        /// <summary>
        /// 注入引擎日志后端。
        /// </summary>
        /// <param name="logger">要注入的日志后端；传入 null 可清空后端。</param>
        public static void SetLogger(IEngineLogger logger)
        {
            lock (sLock)
            {
                if (ReferenceEquals(sLogger, logger))
                    return;

                sLogger = logger;
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 获取当前注入的引擎日志后端。
        /// </summary>
        /// <returns>当前日志后端；未注入时返回 null。</returns>
        public static IEngineLogger GetLogger()
        {
            lock (sLock)
                return sLogger;
        }

        /// <summary>
        /// 清空当前引擎日志后端。
        /// </summary>
        public static void ClearLogger()
        {
            lock (sLock)
            {
                if (sLogger == null)
                    return;

                sLogger = null;
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 重置 LogKit 状态、历史记录和日志后端。
        /// </summary>
        public static void Reset()
        {
            lock (sLock)
            {
                sLogger = null;
                sEnabled = true;
                sMinimumLevel = LogLevel.Debug;
                sDroppedCount = 0;
                sHistory.Clear();
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 写入调试级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象，例如 Unity Object。</param>
        public static void Debug(object message, object context = null)
        {
            Write(LogLevel.Debug, message, context, null);
        }

        /// <summary>
        /// 写入普通信息级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象，例如 Unity Object。</param>
        public static void Log(object message, object context = null)
        {
            Write(LogLevel.Info, message, context, null);
        }

        /// <summary>
        /// 写入普通信息级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象，例如 Unity Object。</param>
        public static void Info(object message, object context = null)
        {
            Write(LogLevel.Info, message, context, null);
        }

        /// <summary>
        /// 写入警告级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象，例如 Unity Object。</param>
        public static void Warning(object message, object context = null)
        {
            Write(LogLevel.Warning, message, context, null);
        }

        /// <summary>
        /// 写入错误级别日志。
        /// </summary>
        /// <param name="message">日志内容；传入异常时会记录异常信息。</param>
        /// <param name="context">宿主上下文对象，例如 Unity Object。</param>
        public static void Error(object message, object context = null)
        {
            Write(LogLevel.Error, message, context, message as Exception);
        }

        /// <summary>
        /// 写入异常日志。
        /// </summary>
        /// <param name="exception">要记录的异常。</param>
        /// <param name="context">宿主上下文对象，例如 Unity Object。</param>
        public static void Exception(Exception exception, object context = null)
        {
            if (exception == null)
            {
                Write(LogLevel.Error, "null exception", context, null);
                return;
            }

            Write(LogLevel.Error, exception, context, exception);
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入调试级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("UNITY_ENABLE_CHECKS")]
        public static void DebugLog(string message)
        {
            Debug(message);
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入警告级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("UNITY_ENABLE_CHECKS")]
        public static void DebugWarning(string message)
        {
            Warning(message);
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入错误级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("UNITY_ENABLE_CHECKS")]
        public static void DebugError(string message)
        {
            Error(message);
        }

        /// <summary>
        /// 获取日志历史快照，最新日志位于结果列表前端。
        /// </summary>
        /// <param name="result">用于接收结果的列表；方法会先清空该列表。</param>
        public static void GetHistory(List<LogKitEntry> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                var entries = sHistory.ToArray();
                for (var i = entries.Length - 1; i >= 0; i--)
                    result.Add(CloneEntry(entries[i]));
            }
        }

        /// <summary>
        /// 清空日志历史和丢弃计数。
        /// </summary>
        public static void ClearHistory()
        {
            lock (sLock)
            {
                sHistory.Clear();
                sDroppedCount = 0;
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 获取当前 LogKit 运行状态快照。
        /// </summary>
        /// <returns>当前日志后端、开关、等级和历史统计。</returns>
        public static LogKitStats GetStats()
        {
            lock (sLock)
            {
                return new LogKitStats
                {
                    LoggerName = sLogger != null ? sLogger.GetType().Name : "None",
                    HasLogger = sLogger != null,
                    Enabled = sEnabled,
                    MinimumLevel = sMinimumLevel,
                    HistoryCount = sHistory.Count,
                    DroppedCount = sDroppedCount
                };
            }
        }

        private static void Write(LogLevel level, object message, object context, Exception exception)
        {
            var normalizedLevel = NormalizeLevel(level);
            var finalMessage = FormatMessage(message);
            IEngineLogger logger;
            lock (sLock)
            {
                if (!sEnabled || normalizedLevel < sMinimumLevel)
                    return;

                EnqueueHistoryLocked(new LogKitEntry
                {
                    Level = normalizedLevel,
                    Message = finalMessage,
                    Context = FormatContext(context),
                    ExceptionType = exception != null ? exception.GetType().Name : string.Empty,
                    ExceptionMessage = exception != null ? exception.Message : string.Empty,
                    StackTrace = exception != null ? exception.StackTrace ?? string.Empty : string.Empty,
                    TimestampUtc = DateTime.UtcNow.ToString("O")
                });
                BumpDiagnosticVersion();
                logger = sLogger;
            }

            if (logger != null)
                logger.Log(normalizedLevel, finalMessage, context);
        }

        private static void EnqueueHistoryLocked(LogKitEntry entry)
        {
            while (sHistory.Count >= MAX_HISTORY)
            {
                sHistory.Dequeue();
                sDroppedCount++;
            }

            sHistory.Enqueue(entry);
        }

        private static void BumpDiagnosticVersion()
        {
            Interlocked.Increment(ref sDiagnosticVersion);
        }

        private static string FormatMessage(object message)
        {
            if (message == null)
                return string.Empty;

            var exception = message as Exception;
            if (exception != null)
                return exception.ToString();

            return message.ToString();
        }

        private static string FormatContext(object context)
        {
            return context != null ? context.ToString() : string.Empty;
        }

        private static LogLevel NormalizeLevel(LogLevel level)
        {
            if (level == LogLevel.Debug ||
                level == LogLevel.Info ||
                level == LogLevel.Warning ||
                level == LogLevel.Error)
            {
                return level;
            }

            return LogLevel.Debug;
        }

        private static LogKitEntry CloneEntry(LogKitEntry entry)
        {
            return new LogKitEntry
            {
                Level = entry.Level,
                Message = entry.Message,
                Context = entry.Context,
                ExceptionType = entry.ExceptionType,
                ExceptionMessage = entry.ExceptionMessage,
                StackTrace = entry.StackTrace,
                TimestampUtc = entry.TimestampUtc
            };
        }
    }
}
