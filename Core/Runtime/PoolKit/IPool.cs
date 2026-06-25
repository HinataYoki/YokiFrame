namespace YokiFrame
{
    /// <summary>
    /// 对象池接口。
    /// </summary>
    public interface IPool<T>
    {
        /// <summary>从对象池分配对象。</summary>
        T Allocate();

        /// <summary>将对象回收到对象池。</summary>
        bool Recycle(T obj);
    }

    /// <summary>
    /// 供调试工具按 object 归还对象的内部适配接口，避免在监控路径使用反射。
    /// </summary>
    internal interface IPoolDebugReturn
    {
        bool TryRecycleObject(object obj);
    }
}
