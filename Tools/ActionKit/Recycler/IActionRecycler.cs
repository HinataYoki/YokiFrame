namespace YokiFrame
{
    internal interface IActionRecycler
    {
        void Recycle();
    }
    /// <summary>
    /// 任务回收
    /// </summary>
    internal struct ActionRecycler<T> : IActionRecycler
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
            Pool.Recycle(Action);
            Pool = null;
            Action = default;
        }
    }
}