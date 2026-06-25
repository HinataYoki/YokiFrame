using System.Collections.Generic;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 堆栈追踪服务。默认关闭，仅在诊断面板显式开启后记录 Start 调用来源。
    /// </summary>
    public static class ActionStackTraceService
    {
        private static readonly Dictionary<ulong, StackTrace> sStackTraces = new Dictionary<ulong, StackTrace>(64);

        /// <summary>
        /// 是否启用 Action 启动堆栈追踪。
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// 当前记录的堆栈数量。
        /// </summary>
        public static int Count
        {
            get { return sStackTraces.Count; }
        }

        /// <summary>
        /// 注册指定 Action 的启动堆栈。
        /// </summary>
        /// <param name="actionId">Action 运行时编号。</param>
        /// <param name="stackTrace">启动堆栈。</param>
        public static void Register(ulong actionId, StackTrace stackTrace)
        {
            if (!Enabled || actionId == 0 || stackTrace == null)
                return;

            sStackTraces[actionId] = stackTrace;
        }

        /// <summary>
        /// 尝试获取指定 Action 的启动堆栈。
        /// </summary>
        /// <param name="actionId">Action 运行时编号。</param>
        /// <param name="stackTrace">启动堆栈。</param>
        public static bool TryGet(ulong actionId, out StackTrace stackTrace)
        {
            return sStackTraces.TryGetValue(actionId, out stackTrace);
        }

        /// <summary>
        /// 移除指定 Action 的启动堆栈。
        /// </summary>
        /// <param name="actionId">Action 运行时编号。</param>
        public static void Remove(ulong actionId)
        {
            sStackTraces.Remove(actionId);
        }

        /// <summary>
        /// 清理所有启动堆栈。
        /// </summary>
        public static void Clear()
        {
            sStackTraces.Clear();
        }
    }
}
