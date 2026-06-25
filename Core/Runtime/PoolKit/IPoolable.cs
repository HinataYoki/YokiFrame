namespace YokiFrame
{
    public interface IPoolable
    {
        /// <summary>对象是否已经回收。</summary>
        bool IsRecycled { get; set; }

        /// <summary>对象回收时调用。</summary>
        void OnRecycled();
    }
}
