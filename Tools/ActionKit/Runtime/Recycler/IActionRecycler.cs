using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 任务回收器 - 结构体避免堆分配
    /// </summary>
    public struct ActionRecycler<T>
    {
        public SimplePoolKit<T> Pool;
        public T Action;

        public ActionRecycler(SimplePoolKit<T> pool, T action)
        {
            Pool = pool;
            Action = action;
        }

        public void Recycle()
        {
            if (Pool != default)
            {
                Pool.Recycle(Action);
            }
            Pool = null;
            Action = default;
        }
    }

    /// <summary>
    /// 泛型回收队列 - 每种 Action 类型独立队列，避免装箱
    /// </summary>
    internal static class RecycleQueue<T>
    {
        private static readonly List<ActionRecycler<T>> sQueue = new(32);
        private static bool sHasPendingRecycle;

        public static void Add(ActionRecycler<T> recycler)
        {
            sQueue.Add(recycler);
            sHasPendingRecycle = true;
        }

        /// <summary>
        /// 检查并处理回收队列
        /// </summary>
        public static void ProcessIfNeeded()
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
                    UnityEngine.Debug.LogError($"[ActionKit] 回收 {typeof(T).Name} 异常: {e.Message}");
                }
            }
            sQueue.Clear();
            sHasPendingRecycle = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// [编辑器专用] 清理队列（PlayMode 退出时调用）
        /// </summary>
        public static void EditorCleanup()
        {
            ProcessIfNeeded();
        }
#endif
    }

    /// <summary>
    /// 回收管理器 - 提供统一的回收接口
    /// </summary>
    public static class ActionRecyclerManager
    {
        /// <summary>
        /// 所有回收队列的处理器（通过反射调用）
        /// </summary>
        private static readonly List<Action> sProcessors = new();

        /// <summary>
        /// 添加回收任务 - 泛型方法避免装箱
        /// </summary>
        public static void AddRecycleCallback<T>(ActionRecycler<T> recycler)
        {
            RecycleQueue<T>.Add(recycler);
        }

        /// <summary>
        /// 注册回收队列处理器（每种类型注册一次）
        /// </summary>
        internal static void RegisterProcessor<T>()
        {
            var processor = new Action(RecycleQueue<T>.ProcessIfNeeded);
            // 直接添加，不检查重复（静态构造函数保证每类型仅调用一次）
            sProcessors.Add(processor);
        }

        /// <summary>
        /// 处理所有待回收队列
        /// </summary>
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
                    UnityEngine.Debug.LogError($"[ActionKit] 回收处理器异常: {e.Message}");
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// [编辑器专用] 清理所有队列
        /// </summary>
        internal static void EditorCleanupAll()
        {
            ProcessAll();
            sProcessors.Clear();
        }
#endif
    }
}