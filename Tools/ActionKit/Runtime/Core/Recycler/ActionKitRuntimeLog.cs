using System;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 核心只暴露日志钩子，具体宿主可在 Adapter 中接入引擎日志。
    /// </summary>
    public static class ActionKitRuntimeLog
    {
        /// <summary>
        /// 宿主侧错误日志处理器。
        /// </summary>
        public static Action<string> ErrorHandler { get; set; }

        /// <summary>
        /// 输出 ActionKit 错误日志。
        /// </summary>
        /// <param name="message">错误消息。</param>
        public static void Error(string message)
        {
            if (ErrorHandler != null)
                ErrorHandler.Invoke(message);
        }
    }
}
