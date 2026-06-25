using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 泛型回收队列，每种 Action 类型独立队列，避免装箱。
    /// </summary>
    internal static class RecycleQueue<T>
    {
        private static readonly List<ActionRecycler<T>> sQueue = new(32);
        private static bool sHasPendingRecycle;

        internal static void Add(ActionRecycler<T> recycler)
        {
            sQueue.Add(recycler);
            sHasPendingRecycle = true;
        }

        internal static void ProcessIfNeeded()
        {
            if (!sHasPendingRecycle || sQueue.Count == 0) return;

            for (int i = 0; i < sQueue.Count; i++)
            {
                try
                {
                    sQueue[i].Recycle();
                }
                catch (Exception e)
                {
                    ActionKitRuntimeLog.Error("[ActionKit] 回收 " + typeof(T).Name + " 异常: " + e.Message);
                }
            }
            sQueue.Clear();
            sHasPendingRecycle = false;
        }

#if UNITY_EDITOR
        internal static void EditorCleanup() => ProcessIfNeeded();
#endif
    }
}
