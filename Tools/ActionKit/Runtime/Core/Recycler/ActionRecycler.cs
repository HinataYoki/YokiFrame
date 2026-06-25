using System;

namespace YokiFrame
{
    /// <summary>
    /// 任务回收器，结构体避免堆分配。
    /// </summary>
    public struct ActionRecycler<T>
    {
        /// <summary>
        /// 回收目标使用的对象池。
        /// </summary>
        public YokiFrame.SimplePoolKit<T> Pool;

        /// <summary>
        /// 等待回收的 Action 实例。
        /// </summary>
        public T Action;

        /// <summary>
        /// 创建任务回收器。
        /// </summary>
        /// <param name="pool">回收目标使用的对象池。</param>
        /// <param name="action">等待回收的 Action 实例。</param>
        public ActionRecycler(YokiFrame.SimplePoolKit<T> pool, T action)
        {
            Pool = pool;
            Action = action;
        }

        /// <summary>
        /// 回收 Action 并清理引用。
        /// </summary>
        public void Recycle()
        {
            if (Pool != null)
                Pool.Recycle(Action);
            Pool = null;
            Action = default;
        }
    }
}
