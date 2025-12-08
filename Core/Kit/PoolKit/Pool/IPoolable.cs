namespace YokiFrame
{
    public interface IPoolable
    {
        /// <summary>
        /// 是否被回收
        /// </summary>
        bool IsRecycled { get; set; }
        /// <summary>
        /// 回收释放
        /// </summary>
        void OnRecycled();
    }
}