#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// 按需记录活动根 Action 的 Start 堆栈；默认关闭并使用固定容量上限。
    /// </summary>
    public static class ActionStackTraceService
    {
        private const int MAX_STACK_TRACES = 256;
        private static readonly object sSyncRoot = new();
        private static readonly Dictionary<ulong, StackTrace> sStackTraces = new(64);
        private static bool sEnabled;

        /// <summary>获取或设置是否为后续 Start 捕获堆栈；关闭时立即释放现有记录。</summary>
        public static bool Enabled
        {
            get => sEnabled;
            set
            {
                if (sEnabled == value) return;
                sEnabled = value;
                if (!value) Clear();
                ActionKitScheduler.NotifyStateChanged();
            }
        }

        /// <summary>获取当前活动堆栈记录数量。</summary>
        public static int Count
        {
            get { lock (sSyncRoot) return sStackTraces.Count; }
        }

        /// <summary>
        /// 为活动根注册堆栈；达到上限后停止新增，避免诊断功能形成无界内存占用。
        /// </summary>
        /// <param name="actionId">非零根 Action ID。</param>
        /// <param name="stackTrace">Start 调用堆栈。</param>
        public static void Register(ulong actionId, StackTrace stackTrace)
        {
            if (!sEnabled || actionId == 0 || stackTrace == null) return;
            lock (sSyncRoot)
            {
                if (sStackTraces.Count >= MAX_STACK_TRACES && !sStackTraces.ContainsKey(actionId)) return;
                sStackTraces[actionId] = stackTrace;
            }
            ActionKitScheduler.NotifyStateChanged();
        }

        /// <summary>
        /// 尝试读取活动根堆栈。
        /// </summary>
        /// <param name="actionId">根 Action ID。</param>
        /// <param name="stackTrace">匹配记录。</param>
        /// <returns>存在记录时返回 true。</returns>
        public static bool TryGet(ulong actionId, out StackTrace stackTrace)
        {
            lock (sSyncRoot) return sStackTraces.TryGetValue(actionId, out stackTrace);
        }

        /// <summary>
        /// 在正常完成、取消或故障时移除对应根堆栈。
        /// </summary>
        /// <param name="actionId">根 Action ID。</param>
        public static void Remove(ulong actionId)
        {
            bool removed;
            lock (sSyncRoot) removed = sStackTraces.Remove(actionId);
            if (removed) ActionKitScheduler.NotifyStateChanged();
        }

        /// <summary>释放全部堆栈记录。</summary>
        public static void Clear()
        {
            lock (sSyncRoot) sStackTraces.Clear();
            ActionKitScheduler.NotifyStateChanged();
        }
    }
}
#endif
