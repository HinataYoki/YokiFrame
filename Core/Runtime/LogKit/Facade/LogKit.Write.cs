using System;
#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 承载 LogKit 写入逻辑；历史队列和状态快照只在 Editor/Tools 编译中存在。
    /// </summary>
    public static partial class LogKit
    {
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 获取日志历史快照，最新日志位于结果列表前端。
        /// </summary>
        /// <param name="result">用于接收结果的列表；方法会先清空该列表。</param>
        public static void GetHistory(List<LogKitEntry> result)
        {
            if (result == null)
            {
                return;
            }

            result.Clear();
            lock (sLock)
            {
                var entries = sHistory.ToArray();
                for (var index = entries.Length - 1; index >= 0; index--)
                {
                    result.Add(CloneEntry(entries[index]));
                }
            }
        }

        /// <summary>
        /// 直接复制最新指定数量的历史，避免 Workbench 高频快照先复制完整队列再裁剪。
        /// </summary>
        /// <param name="maxCount">最多返回的历史数量；小于等于零时返回空数组。</param>
        /// <param name="stats">与返回历史在同一锁内捕获的统计。</param>
        /// <param name="diagnosticVersion">与返回历史对应的诊断版本。</param>
        /// <returns>最新日志位于数组前端的独立条目数组。</returns>
        internal static LogKitEntry[] GetRecentHistory(
            int maxCount,
            out LogKitStats stats,
            out long diagnosticVersion)
        {
            lock (sLock)
            {
                stats = CreateStatsLocked();
                diagnosticVersion = sDiagnosticVersion;
                if (maxCount <= 0)
                {
                    return Array.Empty<LogKitEntry>();
                }

                int count = Math.Min(maxCount, sHistory.Count);
                LogKitEntry[] result = new LogKitEntry[count];
                int skipCount = sHistory.Count - count;
                int queueIndex = 0;
                int destinationIndex = count - 1;
                foreach (LogKitEntry entry in sHistory)
                {
                    if (queueIndex++ < skipCount)
                    {
                        continue;
                    }

                    result[destinationIndex--] = CloneEntry(entry);
                }

                return result;
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
                return CreateStatsLocked();
            }
        }

        /// <summary>在持有 LogKit 锁时创建与当前历史严格一致的统计对象。</summary>
        private static LogKitStats CreateStatsLocked()
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
#endif

        /// <summary>
        /// 写入日志并转发给宿主后端；Editor/Tools 编译时同时记录观察历史。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文。</param>
        /// <param name="exception">异常对象；非异常日志传 null。</param>
        private static void Write(LogLevel level, object message, object context, Exception exception)
        {
            EnsureDefaultLogger();
            LogLevel normalizedLevel = NormalizeLevel(level);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (!ShouldWrite(normalizedLevel))
            {
                return;
            }

            string finalMessage = FormatMessage(message);
            string stackTrace = ResolveStackTrace(exception);
            IEngineLogger logger = EnqueueAndGetLogger(normalizedLevel, finalMessage, context, exception, stackTrace);
            WriteToLogger(logger, normalizedLevel, finalMessage, context, stackTrace);
#else
            IEngineLogger logger = GetLoggerAfterFilter(normalizedLevel);
            if (logger == null)
            {
                return;
            }

            string finalMessage = FormatMessage(message);
            logger.Log(normalizedLevel, finalMessage, context);
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 在格式化消息前执行轻量过滤，避免被禁用日志产生无意义分配。
        /// </summary>
        /// <param name="level">已经归一化的日志等级。</param>
        /// <returns>当前设置允许该日志时返回 true。</returns>
        private static bool ShouldWrite(LogLevel level)
        {
            lock (sLock)
            {
                return sEnabled && level >= sMinimumLevel;
            }
        }
#endif

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 在锁内完成二次过滤、历史入队和后端读取，避免状态变化造成半写入。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">格式化后的日志内容。</param>
        /// <param name="context">宿主上下文。</param>
        /// <param name="exception">异常对象。</param>
        /// <param name="stackTrace">调用点堆栈。</param>
        /// <returns>当前有效日志后端；日志被过滤时返回 null。</returns>
        private static IEngineLogger EnqueueAndGetLogger(LogLevel level, string message, object context, Exception exception, string stackTrace)
        {
            lock (sLock)
            {
                if (!sEnabled || level < sMinimumLevel)
                {
                    return null;
                }

                EnqueueHistoryLocked(CreateEntry(level, message, context, exception, stackTrace));
                BumpDiagnosticVersion();
                return sLogger;
            }
        }

        /// <summary>
        /// 创建历史日志条目。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">格式化后的日志内容。</param>
        /// <param name="context">宿主上下文。</param>
        /// <param name="exception">异常对象。</param>
        /// <param name="stackTrace">调用点堆栈。</param>
        /// <returns>历史日志条目。</returns>
        private static LogKitEntry CreateEntry(LogLevel level, string message, object context, Exception exception, string stackTrace)
        {
            return new LogKitEntry
            {
                Level = level,
                Message = message,
                Context = FormatContext(context),
                ExceptionType = exception != null ? exception.GetType().Name : string.Empty,
                ExceptionMessage = exception != null ? exception.Message : string.Empty,
                StackTrace = stackTrace,
                TimestampUtc = DateTime.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 把日志转发给宿主后端，支持带堆栈和不带堆栈两种后端。
        /// </summary>
        /// <param name="logger">宿主日志后端。</param>
        /// <param name="level">日志等级。</param>
        /// <param name="message">格式化后的日志内容。</param>
        /// <param name="context">宿主上下文。</param>
        /// <param name="stackTrace">调用点堆栈。</param>
        private static void WriteToLogger(IEngineLogger logger, LogLevel level, string message, object context, string stackTrace)
        {
            if (logger == null)
            {
                return;
            }

            var stackTraceLogger = logger as IEngineLoggerWithStackTrace;
            if (stackTraceLogger != null)
            {
                stackTraceLogger.Log(level, message, context, stackTrace);
                return;
            }

            logger.Log(level, message, context);
        }

        /// <summary>
        /// 将日志加入固定容量历史队列，超出容量时丢弃最旧日志。
        /// </summary>
        /// <param name="entry">待加入历史的日志条目。</param>
        private static void EnqueueHistoryLocked(LogKitEntry entry)
        {
            while (sHistory.Count >= MAX_HISTORY)
            {
                sHistory.Dequeue();
                sDroppedCount++;
            }

            sHistory.Enqueue(entry);
        }
#else
        /// <summary>
        /// 在 Player 热路径内一次性确认过滤状态并读取后端，不创建任何 Workbench 观察对象。
        /// </summary>
        /// <param name="level">已经归一化的日志等级。</param>
        /// <returns>当前有效日志后端；状态变化后不再允许写入时返回 null。</returns>
        private static IEngineLogger GetLoggerAfterFilter(LogLevel level)
        {
            lock (sLock)
            {
                return sEnabled && level >= sMinimumLevel ? sLogger : null;
            }
        }
#endif

        /// <summary>
        /// 设置启用状态；Editor/Tools 编译时在变化后同步递增观察版本。
        /// </summary>
        /// <param name="value">新的启用状态。</param>
        private static void SetEnabled(bool value)
        {
            lock (sLock)
            {
                if (sEnabled == value)
                {
                    return;
                }

                sEnabled = value;
#if UNITY_EDITOR || (GODOT && TOOLS)
                BumpDiagnosticVersion();
#endif
            }
        }

        /// <summary>
        /// 设置最小日志等级；Editor/Tools 编译时在变化后同步递增观察版本。
        /// </summary>
        /// <param name="value">新的最小日志等级。</param>
        private static void SetMinimumLevel(LogLevel value)
        {
            lock (sLock)
            {
                LogLevel normalized = NormalizeLevel(value);
                if (sMinimumLevel == normalized)
                {
                    return;
                }

                sMinimumLevel = normalized;
#if UNITY_EDITOR || (GODOT && TOOLS)
                BumpDiagnosticVersion();
#endif
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 递增 Editor/Tools 诊断版本。
        /// </summary>
        private static void BumpDiagnosticVersion()
        {
            System.Threading.Interlocked.Increment(ref sDiagnosticVersion);
        }
#endif
    }
}
