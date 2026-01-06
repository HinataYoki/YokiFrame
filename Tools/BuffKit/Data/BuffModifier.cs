namespace YokiFrame
{
    /// <summary>
    /// 属性修改器，定义 Buff 对属性的修改方式
    /// </summary>
    public struct BuffModifier
    {
        /// <summary>
        /// 属性 ID（配置表 ID）
        /// </summary>
        public int AttributeId;
        
        /// <summary>
        /// 修改类型
        /// </summary>
        public ModifierType Type;
        
        /// <summary>
        /// 修改值
        /// </summary>
        public float Value;
        
        /// <summary>
        /// 优先级（同类型内排序，数值越大优先级越高）
        /// </summary>
        public int Priority;

        public BuffModifier(int attributeId, ModifierType type, float value, int priority = 0)
        {
            AttributeId = attributeId;
            Type = type;
            Value = value;
            Priority = priority;
        }
    }
}
