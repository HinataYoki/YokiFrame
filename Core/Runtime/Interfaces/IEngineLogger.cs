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
}
