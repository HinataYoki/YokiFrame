namespace YokiFrame
{
    /// <summary>
    /// Buff 添加事件
    /// </summary>
    public struct BuffAddedEvent
    {
        public BuffContainer Container;
        public BuffInstance Instance;
    }

    /// <summary>
    /// Buff 移除事件
    /// </summary>
    public struct BuffRemovedEvent
    {
        public BuffContainer Container;
        public BuffInstance Instance;
        public BuffRemoveReason Reason;
    }

    /// <summary>
    /// Buff 堆叠变化事件
    /// </summary>
    public struct BuffStackChangedEvent
    {
        public BuffContainer Container;
        public BuffInstance Instance;
        public int OldStack;
        public int NewStack;
    }
}
