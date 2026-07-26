using System;
#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Diagnostics;
using System.Text;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 承载 LogKit 消息格式化和调用方堆栈捕获逻辑。
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
        /// 优先使用异常自身堆栈，否则捕获当前调用方堆栈。
        /// </summary>
        /// <param name="exception">异常对象。</param>
        /// <returns>调用方堆栈。</returns>
        private static string ResolveStackTrace(Exception exception)
        {
            if (exception != null && !string.IsNullOrEmpty(exception.StackTrace))
            {
                return exception.StackTrace;
            }

            return CaptureCallerStackTrace();
        }

        /// <summary>
        /// 捕获调用方堆栈，并过滤 LogKit 自身包装层。
        /// </summary>
        /// <returns>过滤后的调用方堆栈。</returns>
        private static string CaptureCallerStackTrace()
        {
            var stackTrace = new StackTrace(2, true);
            StackFrame[] frames = stackTrace.GetFrames();
            if (frames == null || frames.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(256);
            AppendCallerFrames(builder, frames);
            return builder.ToString();
        }

        /// <summary>
        /// 将非 LogKit 包装层的调用帧追加到堆栈文本。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="frames">调用栈帧数组。</param>
        private static void AppendCallerFrames(StringBuilder builder, StackFrame[] frames)
        {
            for (var index = 0; index < frames.Length; index++)
            {
                StackFrame frame = frames[index];
                if (frame == null || IsLogKitFrame(frame))
                {
                    continue;
                }

                AppendStackFrame(builder, frame);
            }
        }

        /// <summary>
        /// 判断调用帧是否来自 LogKit 包装层。
        /// </summary>
        /// <param name="frame">调用栈帧。</param>
        /// <returns>来自包装层时返回 true。</returns>
        private static bool IsLogKitFrame(StackFrame frame)
        {
            var method = frame.GetMethod();
            Type declaringType = method != null ? method.DeclaringType : null;
            return declaringType == typeof(LogKit);
        }

        /// <summary>
        /// 追加单个调用栈帧文本。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="frame">调用栈帧。</param>
        private static void AppendStackFrame(StringBuilder builder, StackFrame frame)
        {
            var method = frame.GetMethod();
            Type declaringType = method != null ? method.DeclaringType : null;
            string typeName = declaringType != null ? declaringType.FullName : "<unknown>";
            string methodName = method != null ? method.Name : "<unknown>";

            builder.Append(typeName.Replace('+', '/'));
            builder.Append(':');
            builder.Append(methodName);
            builder.Append(" ()");
            AppendSourceLocation(builder, frame);
            builder.AppendLine();
        }

        /// <summary>
        /// 追加源码位置，无法获取文件名或行号时保持 Unity 风格的简短堆栈。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="frame">调用栈帧。</param>
        private static void AppendSourceLocation(StringBuilder builder, StackFrame frame)
        {
            string fileName = frame.GetFileName();
            int lineNumber = frame.GetFileLineNumber();
            if (string.IsNullOrEmpty(fileName) || lineNumber <= 0)
            {
                return;
            }

            builder.Append(" (at ");
            builder.Append(NormalizeStackTraceFileName(fileName));
            builder.Append(':');
            builder.Append(lineNumber);
            builder.Append(')');
        }

        /// <summary>
        /// 归一化堆栈文件名，项目内路径优先显示 Assets 相对路径。
        /// </summary>
        /// <param name="fileName">原始文件名。</param>
        /// <returns>归一化后的文件名。</returns>
        private static string NormalizeStackTraceFileName(string fileName)
        {
            string normalized = fileName.Replace('\\', '/');
            int assetsIndex = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? normalized.Substring(assetsIndex + 1) : normalized;
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
