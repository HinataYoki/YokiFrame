namespace YokiFrame
{
    /// <summary>
    /// LogKit 运行状态快照。
    /// </summary>
    public sealed class LogKitStats
    {
        /// <summary>
        /// 当前日志后端类型名称。
        /// </summary>
        public string LoggerName;

        /// <summary>
        /// 当前是否已经注入日志后端。
        /// </summary>
        public bool HasLogger;

        /// <summary>
        /// 当前是否启用日志转发。
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// 当前最小日志等级。
        /// </summary>
        public LogLevel MinimumLevel;

        /// <summary>
        /// 当前保留的历史日志数量。
        /// </summary>
        public int HistoryCount;

        /// <summary>
        /// 因历史容量限制被丢弃的日志数量。
        /// </summary>
        public int DroppedCount;
    }
}
