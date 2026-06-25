using System;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时异常的全局错误处理器。
    /// Adapter 层可以设置该委托，把错误转发到宿主专用日志（例如 Debug.LogError）。
    /// Base 层永远不直接引用任何日志框架。
    /// </summary>
    public static class EventKitErrorHandler
    {
        /// <summary>
        /// 事件监听器抛出异常时调用。
        /// 由 Adapter 层设置，用于接入宿主专用日志。
        /// </summary>
        public static Action<string> OnError;
    }
}
