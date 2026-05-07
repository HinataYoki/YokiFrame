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
        Stack,

        /// <summary>
        /// 最强覆盖模式：使用 StrengthComparer 比较，更强者覆盖弱者，弱者被丢弃，相同则刷新时间
        /// </summary>
        StrongestWins
    }
}
