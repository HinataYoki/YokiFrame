namespace YokiFrame
{
    /// <summary>
    /// 由内置 Action 实现的延迟回池入口，避免对自定义 Action 假定所有权。
    /// </summary>
    internal interface IPooledAction
    {
        /// <summary>将已完成 OnDeinit 的实例归还对应局部 PoolKit 池。</summary>
        void ReturnToPool();
    }
}
