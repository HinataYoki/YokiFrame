using System;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 监听器异常的统一处理入口。
    /// </summary>
    public static class EventKitErrorHandler
    {
        /// <summary>
        /// 宿主适配层可注入该委托，把 EventKit 异常转发到 Unity、Godot 或其它宿主日志。
        /// </summary>
        public static Action<string> OnError;

        /// <summary>
        /// 上报监听器执行异常；未注入宿主处理器时回退到 Core LogKit。
        /// </summary>
        /// <param name="message">已格式化的错误信息。</param>
        public static void Report(string message)
        {
            Action<string> errorHandler = OnError;
            if (errorHandler == null)
            {
                LogKit.Error(message);
                return;
            }

            Delegate[] callbacks = errorHandler.GetInvocationList();
            for (int index = 0; index < callbacks.Length; index++)
            {
                try
                {
                    ((Action<string>)callbacks[index]).Invoke(message);
                }
                catch (Exception exception)
                {
                    ReportHandlerFailure(exception);
                }
            }
        }

        /// <summary>
        /// 记录宿主错误处理器自身的异常；该异常不能反向中断事件派发。
        /// </summary>
        /// <param name="exception">错误处理器抛出的异常。</param>
        private static void ReportHandlerFailure(Exception exception)
        {
            try
            {
                LogKit.Error("[EventKit] Error handler failed: " + exception.Message);
            }
            catch (Exception loggingException)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[EventKit] Error handler and fallback logger failed: " + loggingException.Message);
            }
        }
    }
}
