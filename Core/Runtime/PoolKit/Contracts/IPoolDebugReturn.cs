#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 供 PoolDebugger 以 object 形式强制归还对象的内部契约，避免诊断路径使用反射。
    /// </summary>
    internal interface IPoolDebugReturn
    {
        /// <summary>
        /// 尝试把对象按真实类型归还到当前对象池。
        /// </summary>
        /// <param name="obj">需要强制归还的对象。</param>
        /// <returns>归还成功时返回 true。</returns>
        bool TryRecycleObject(object obj);
    }
}
#endif
