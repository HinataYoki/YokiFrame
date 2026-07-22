namespace YokiFrame
{
    /// <summary>
    /// 对象池的最小分配与回收契约。
    /// </summary>
    /// <typeparam name="T">池化对象类型。</typeparam>
    public interface IPool<T>
    {
        /// <summary>
        /// 从对象池分配一个对象；池为空时由对象工厂创建。
        /// </summary>
        /// <returns>可用对象。</returns>
        T Allocate();

        /// <summary>
        /// 将对象归还到对象池。
        /// </summary>
        /// <param name="obj">需要归还的对象。</param>
        /// <returns>对象被池接收或缓存时返回 true；对象无效、重复归还或超过缓存容量时返回 false。</returns>
        bool Recycle(T obj);
    }
}
