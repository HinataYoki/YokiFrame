namespace YokiFrame
{
    /// <summary>
    /// Buff 堆叠模式
    /// </summary>
    public enum StackMode
    {
        /// <summary>
        /// 独立模式：每次添加创建新实例
        /// </summary>
        Independent,
        
        /// <summary>
        /// 刷新模式：重置已有实例的持续时间
        /// </summary>
        Refresh,
        
        /// <summary>
        /// 堆叠模式：增加堆叠层数
        /// </summary>
        Stack
    }
}
