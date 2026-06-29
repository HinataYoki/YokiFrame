namespace YokiFrame
{
    /// <summary>
    /// 引擎日志抽象接口，替代 Unity 的 Debug.Log
    /// </summary>
    public interface IEngineLogger
    {
        /// <summary>
        /// 写入指定等级的日志。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象。</param>
        void Log(LogLevel level, string message, object context = null);
    }

    /// <summary>
    /// 可接收调用点堆栈的引擎日志扩展接口。旧 Logger 可以继续只实现 IEngineLogger。
    /// </summary>
    public interface IEngineLoggerWithStackTrace : IEngineLogger
    {
        /// <summary>
        /// 写入指定等级的日志，并附带 LogKit 调用方堆栈。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象。</param>
        /// <param name="stackTrace">已过滤掉 LogKit 包装层的调用方堆栈。</param>
        void Log(LogLevel level, string message, object context, string stackTrace);
    }
}
