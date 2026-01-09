namespace YokiFrame
{
    /// <summary>
    /// 任务回收器 - 结构体避免堆分配
    /// </summary>
    internal struct ActionRecycler<T>
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
            Pool?.Recycle(Action);
            Pool = null;
            Action = default;
        }
    }
}