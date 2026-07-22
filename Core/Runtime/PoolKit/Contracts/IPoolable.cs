namespace YokiFrame
{
    /// <summary>
    /// 对象池标准生命周期契约，用于省略重复的创建和生命周期委托配置。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 对象从池中借出后调用，用于恢复通用可用状态。
        /// </summary>
        void OnAllocated();

        /// <summary>
        /// 对象归还到池中或被容量限制丢弃前调用，用于清理通用状态。
        /// </summary>
        void OnRecycled();
    }
}
