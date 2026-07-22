#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// PoolDebugger 的时间和堆栈解析辅助逻辑。
    /// </summary>
    public static partial class PoolDebugger
    {
        /// <summary>
        /// 获取从诊断器加载到当前的秒数。
        /// </summary>
        /// <returns>相对秒数。</returns>
        private static float GetElapsedSeconds()
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - sStartTimestamp;
            return (float)(elapsedTicks / (double)Stopwatch.Frequency);
        }

        /// <summary>
        /// 从堆栈文本中提取第一个业务调用行。
        /// </summary>
        /// <param name="stackTrace">堆栈文本。</param>
        /// <returns>调用来源文本。</returns>
        private static string ParseStackTraceSource(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return string.Empty;
            }

            string[] lines = stackTrace.Split('\n');
            return FindFirstExternalStackLine(lines);
        }

        /// <summary>
        /// 从 StackTrace 对象或文本中解析调用位置。
        /// </summary>
        /// <param name="stackTraceObject">StackTrace 对象。</param>
        /// <param name="stackTrace">堆栈文本。</param>
        /// <returns>调用位置。</returns>
        private static SourceLocation ParseStackTraceLocation(StackTrace stackTraceObject, string stackTrace)
        {
            SourceLocation location = ParseStackTraceObjectLocation(stackTraceObject);
            if (!string.IsNullOrEmpty(location.FilePath))
            {
                return location;
            }

            return ParseStackTraceTextLocation(stackTrace);
        }

        /// <summary>
        /// 从 StackTrace 对象中解析调用位置。
        /// </summary>
        /// <param name="stackTraceObject">StackTrace 对象。</param>
        /// <returns>调用位置。</returns>
        private static SourceLocation ParseStackTraceObjectLocation(StackTrace stackTraceObject)
        {
            if (stackTraceObject == null)
            {
                return default;
            }

            StackFrame[] frames = stackTraceObject.GetFrames();
            return frames == null ? default : FindFirstExternalFrame(frames);
        }

        /// <summary>
        /// 从堆栈文本中解析调用位置。
        /// </summary>
        /// <param name="stackTrace">堆栈文本。</param>
        /// <returns>调用位置。</returns>
        private static SourceLocation ParseStackTraceTextLocation(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return default;
            }

            string[] lines = stackTrace.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                SourceLocation location = TryParseLocationLine(lines[index]);
                if (!string.IsNullOrEmpty(location.FilePath))
                {
                    return location;
                }
            }

            return default;
        }

        /// <summary>
        /// 查找第一个非 PoolKit 内部调用行。
        /// </summary>
        /// <param name="lines">堆栈行。</param>
        /// <returns>调用行文本。</returns>
        private static string FindFirstExternalStackLine(string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (IsInternalPoolStackLine(line))
                {
                    continue;
                }

                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 查找第一个非 PoolKit 内部栈帧。
        /// </summary>
        /// <param name="frames">栈帧数组。</param>
        /// <returns>调用位置。</returns>
        private static SourceLocation FindFirstExternalFrame(StackFrame[] frames)
        {
            for (var index = 0; index < frames.Length; index++)
            {
                string typeName = GetFrameTypeName(frames[index]);
                if (IsInternalPoolFrame(typeName))
                {
                    continue;
                }

                string filePath = frames[index].GetFileName();
                if (!string.IsNullOrEmpty(filePath))
                {
                    return new SourceLocation(filePath.Replace('\\', '/'), frames[index].GetFileLineNumber());
                }
            }

            return default;
        }

        /// <summary>
        /// 获取栈帧声明类型名。
        /// </summary>
        /// <param name="frame">栈帧。</param>
        /// <returns>声明类型名。</returns>
        private static string GetFrameTypeName(StackFrame frame)
        {
            var method = frame.GetMethod();
            return method != null && method.DeclaringType != null ? method.DeclaringType.FullName : string.Empty;
        }

        /// <summary>
        /// 尝试从单行堆栈文本中解析文件和行号。
        /// </summary>
        /// <param name="line">堆栈行。</param>
        /// <returns>调用位置。</returns>
        private static SourceLocation TryParseLocationLine(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length <= 0 || IsInternalPoolStackLine(trimmed))
            {
                return default;
            }

            int fileMarker = trimmed.IndexOf(" in ", StringComparison.Ordinal);
            int lineMarker = trimmed.LastIndexOf(":line ", StringComparison.Ordinal);
            return TryCreateSourceLocation(trimmed, fileMarker, lineMarker);
        }

        /// <summary>
        /// 根据堆栈文本标记创建调用位置。
        /// </summary>
        /// <param name="line">堆栈行。</param>
        /// <param name="fileMarker">文件路径标记位置。</param>
        /// <param name="lineMarker">行号标记位置。</param>
        /// <returns>调用位置。</returns>
        private static SourceLocation TryCreateSourceLocation(string line, int fileMarker, int lineMarker)
        {
            int fileStart = fileMarker + 4;
            if (fileMarker < 0 || lineMarker <= fileStart)
            {
                return default;
            }

            string filePath = line.Substring(fileStart, lineMarker - fileStart).Replace('\\', '/');
            string lineText = line.Substring(lineMarker + 6).Trim();
            int.TryParse(lineText, out int lineNumber);
            return new SourceLocation(filePath, lineNumber);
        }

        /// <summary>
        /// 判断类型名是否属于 PoolKit 内部栈帧。
        /// </summary>
        /// <param name="typeName">类型名。</param>
        /// <returns>属于内部栈帧时返回 true。</returns>
        private static bool IsInternalPoolFrame(string typeName)
        {
            return string.IsNullOrEmpty(typeName) ||
                   typeName.IndexOf("PoolDebugger", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.PoolKit", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.ObjectPool", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.SharedPoolRegistry", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("System.Diagnostics", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// 判断堆栈行是否属于 PoolKit 内部调用。
        /// </summary>
        /// <param name="line">堆栈行。</param>
        /// <returns>属于内部调用时返回 true。</returns>
        private static bool IsInternalPoolStackLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }

            return line.IndexOf("PoolDebugger", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.PoolKit", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.ObjectPool", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.SharedPoolRegistry", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("System.Diagnostics", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// 调用位置数据。
        /// </summary>
        private readonly struct SourceLocation
        {
            /// <summary>
            /// 调用位置文件。
            /// </summary>
            public readonly string FilePath;

            /// <summary>
            /// 调用位置行号。
            /// </summary>
            public readonly int Line;

            /// <summary>
            /// 创建调用位置。
            /// </summary>
            /// <param name="filePath">文件路径。</param>
            /// <param name="line">行号。</param>
            public SourceLocation(string filePath, int line)
            {
                FilePath = filePath;
                Line = line;
            }
        }
    }
}
#endif
