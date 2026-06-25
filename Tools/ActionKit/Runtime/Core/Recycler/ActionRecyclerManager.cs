using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 回收管理器，提供统一的回收接口。
    /// </summary>
    public static class ActionRecyclerManager
    {
        private static readonly List<Action> sProcessors = new();

        /// <summary>
        /// 添加等待回收的 Action。
        /// </summary>
        /// <param name="recycler">回收器。</param>
        public static void AddRecycleCallback<T>(ActionRecycler<T> recycler)
        {
            RecycleQueue<T>.Add(recycler);
        }

        internal static void RegisterProcessor<T>()
        {
            Action processor = RecycleQueue<T>.ProcessIfNeeded;
            sProcessors.Add(processor);
        }

        internal static void ProcessAll()
        {
            for (int i = 0; i < sProcessors.Count; i++)
            {
                try
                {
                    sProcessors[i]?.Invoke();
                }
                catch (Exception e)
                {
                    ActionKitRuntimeLog.Error("[ActionKit] 回收处理器异常: " + e.Message);
                }
            }
        }

        internal static void EditorCleanupAll()
        {
            ProcessAll();
        }
    }
}
