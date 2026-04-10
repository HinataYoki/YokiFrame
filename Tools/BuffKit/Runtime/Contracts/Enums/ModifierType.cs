namespace YokiFrame
{
    /// <summary>
    /// 属性修改器类型
    /// </summary>
    public enum ModifierType
    {
        /// <summary>
        /// 加法：baseValue + value
        /// </summary>
        Additive,
        
        /// <summary>
        /// 乘法：baseValue * (1 + value)
        /// </summary>
        Multiplicative,
        
        /// <summary>
        /// 覆盖：直接使用 value
        /// </summary>
        Override
    }
}
