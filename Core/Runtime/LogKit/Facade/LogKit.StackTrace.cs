using System;

namespace YokiFrame
{
    /// <summary>
    /// 承载 LogKit 消息格式化与历史条目克隆逻辑；调用点堆栈由宿主原生机制呈现。
    /// </summary>
    public static partial class LogKit
    {
        /// <summary>
        /// 格式化日志内容，异常会展开为完整异常字符串。
        /// </summary>
        /// <param name="message">原始日志内容。</param>
        /// <returns>格式化后的日志内容。</returns>
        private static string FormatMessage(object message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            var exception = message as Exception;
            return exception != null ? exception.ToString() : message.ToString();
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 格式化宿主上下文，Core 不解析宿主对象。
        /// </summary>
        /// <param name="context">宿主上下文。</param>
        /// <returns>格式化后的上下文。</returns>
        private static string FormatContext(object context)
        {
            return context != null ? context.ToString() : string.Empty;
        }
#endif

        /// <summary>
        /// 归一化日志等级，避免非法枚举值破坏过滤规则。
        /// </summary>
        /// <param name="level">原始日志等级。</param>
        /// <returns>有效日志等级。</returns>
        private static LogLevel NormalizeLevel(LogLevel level)
        {
            return NormalizeLevel(level, LogLevel.Debug);
        }

        /// <summary>
        /// 归一化日志等级，非法枚举值回落到指定等级。
        /// </summary>
        /// <param name="level">原始日志等级。</param>
        /// <param name="fallback">非法枚举值时使用的回落等级。</param>
        /// <returns>有效日志等级。</returns>
        internal static LogLevel NormalizeLevel(LogLevel level, LogLevel fallback)
        {
            return level == LogLevel.Debug || level == LogLevel.Info || level == LogLevel.Warning || level == LogLevel.Error
                ? level
                : fallback;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 仅采用异常自身堆栈；普通日志的调用点由宿主原生堆栈呈现，Core 不再重复捕获。
        /// </summary>
        /// <param name="exception">异常对象。</param>
        /// <returns>异常堆栈；非异常日志返回空字符串。</returns>
        private static string ResolveStackTrace(Exception exception)
        {
            return exception != null && !string.IsNullOrEmpty(exception.StackTrace)
                ? exception.StackTrace
                : string.Empty;
        }

        /// <summary>
        /// 克隆历史条目，避免调用方修改 LogKit 内部队列元素。
        /// </summary>
        /// <param name="entry">原始历史条目。</param>
        /// <returns>克隆后的历史条目。</returns>
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
#endif
    }
}
