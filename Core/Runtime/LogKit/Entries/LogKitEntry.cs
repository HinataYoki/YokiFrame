#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 表示 LogKit 保存的一条历史日志快照。
    /// </summary>
    public sealed class LogKitEntry
    {
        /// <summary>
        /// 日志等级。
        /// </summary>
        public LogLevel Level;

        /// <summary>
        /// 格式化后的日志内容。
        /// </summary>
        public string Message;

        /// <summary>
        /// 格式化后的宿主上下文。
        /// </summary>
        public string Context;

        /// <summary>
        /// 异常类型名称；非异常日志为空字符串。
        /// </summary>
        public string ExceptionType;

        /// <summary>
        /// 异常消息；非异常日志为空字符串。
        /// </summary>
        public string ExceptionMessage;

        /// <summary>
        /// 调用点或异常堆栈；无法获取时为空字符串。
        /// </summary>
        public string StackTrace;

        /// <summary>
        /// 日志写入时的 UTC 时间戳。
        /// </summary>
        public string TimestampUtc;
    }
}
#endif
