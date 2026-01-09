namespace YokiFrame
{
    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IPool<T>
    {
        /// <summary>
        /// 分配对象
        /// </summary>
        T Allocate();
        /// <summary>
        /// 回收对象
        /// </summary>
        bool Recycle(T obj);
    }
}