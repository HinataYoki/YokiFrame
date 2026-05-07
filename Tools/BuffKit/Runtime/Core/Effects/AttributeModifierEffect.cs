namespace YokiFrame
{
    /// <summary>
    /// 属性修改效果 - 在 Buff 生效期间修改指定属性。
    /// 属性修改通过 BuffModifier 在 GetModifiedValue 时计算，此 effect 主要用于触发属性变化事件。
    /// </summary>
    public class AttributeModifierEffect : BuffEffectBase
    {
        private readonly int mAttributeId;
        private readonly ModifierType mType;
        private readonly float mValue;

        public AttributeModifierEffect(int attributeId, ModifierType type, float value)
        {
            mAttributeId = attributeId;
            mType = type;
            mValue = value;
        }
    }
}
