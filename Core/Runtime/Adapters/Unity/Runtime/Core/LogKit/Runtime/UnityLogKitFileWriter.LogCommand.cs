#if !GODOT
using System;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 日志文件写入器的日志命令定义。
    /// </summary>
    public static partial class UnityLogKitFileWriter
    {
        /// <summary>
        /// 后台日志写入命令。
        /// </summary>
        public struct LogCommand
        {
            /// <summary>
            /// 日志产生时间。
            /// </summary>
            public DateTime Time;

            /// <summary>
            /// Unity 日志类型。
            /// </summary>
            public LogType Type;

            /// <summary>
            /// 日志内容。
            /// </summary>
            public string Message;

            /// <summary>
            /// Unity 原始堆栈信息。
            /// </summary>
            public string RawStack;
        }
    }
}
#endif
