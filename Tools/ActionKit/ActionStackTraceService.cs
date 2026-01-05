using System.Collections.Generic;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// Action 堆栈追踪服务（仅编辑器有实际实现）
    /// 用于记录 Action 创建时的调用堆栈
    /// </summary>
    public static class ActionStackTraceService
    {
#if UNITY_EDITOR
        private static readonly Dictionary<ulong, StackTrace> sStackTraces = new(64);
        
        /// <summary>
        /// 是否启用堆栈追踪（默认关闭以提升性能）
        /// </summary>
        public static bool Enabled { get; set; } = false;
        
        /// <summary>
        /// 注册 Action 的堆栈信息
        /// </summary>
        public static void Register(ulong actionId, StackTrace stackTrace)
        {
            if (!Enabled) return;
            sStackTraces[actionId] = stackTrace;
        }
        
        /// <summary>
        /// 获取 Action 的堆栈信息
        /// </summary>
        public static bool TryGet(ulong actionId, out StackTrace stackTrace)
        {
            return sStackTraces.TryGetValue(actionId, out stackTrace);
        }
        
        /// <summary>
        /// 移除 Action 的堆栈信息
        /// </summary>
        public static void Remove(ulong actionId)
        {
            sStackTraces.Remove(actionId);
        }
        
        /// <summary>
        /// 清空所有堆栈信息
        /// </summary>
        public static void Clear()
        {
            sStackTraces.Clear();
        }
        
        /// <summary>
        /// 当前记录的堆栈数量
        /// </summary>
        public static int Count => sStackTraces.Count;
#else
        // 运行时空实现
        public static bool Enabled { get; set; } = false;
        public static void Register(ulong actionId, StackTrace stackTrace) { }
        public static bool TryGet(ulong actionId, out StackTrace stackTrace) { stackTrace = null; return false; }
        public static void Remove(ulong actionId) { }
        public static void Clear() { }
        public static int Count => 0;
#endif
    }
}
