namespace YokiFrame
{
    /// <summary>
    /// 集中定义内置 Action 的回收策略，避免公开对象引用跨租约发生 ABA。
    /// </summary>
    internal static class ActionPoolSettings
    {
        /// <summary>获取零预热、零保留配置；PoolKit 仍负责统一 reset 与诊断闭环。</summary>
        internal static PoolOptions Default { get; } = new(0, 0);
    }
}
