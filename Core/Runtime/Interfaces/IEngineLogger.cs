namespace YokiFrame
{
    /// <summary>
    /// 定义宿主日志后端的最小抽象，Core Runtime 通过它转发日志而不直接依赖具体宿主 API。
    /// </summary>
    public interface IEngineLogger
    {
        /// <summary>
        /// 写入一条指定等级的日志。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">已经格式化后的日志内容。</param>
        /// <param name="context">宿主上下文对象；Core 只透传，不解析。</param>
        void Log(LogLevel level, string message, object context = null);
    }

#if UNITY_EDITOR || (GODOT && TOOLS)
    /// <summary>
    /// 定义可接收调用点堆栈的工具日志后端，Player 只使用 <see cref="IEngineLogger"/>。
    /// </summary>
    public interface IEngineLoggerWithStackTrace : IEngineLogger
    {
        /// <summary>
        /// 写入一条指定等级的日志，并附带 LogKit 过滤后的调用方堆栈。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">已经格式化后的日志内容。</param>
        /// <param name="context">宿主上下文对象；Core 只透传，不解析。</param>
        /// <param name="stackTrace">已过滤 LogKit 包装层后的调用堆栈。</param>
        void Log(LogLevel level, string message, object context, string stackTrace);
    }
#endif
}
