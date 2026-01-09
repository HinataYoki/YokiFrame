namespace YokiFrame
{
    /// <summary>
    /// 属性修改效果 - 在 Buff 生效期间修改指定属性
    /// </summary>
    public class AttributeModifierEffect : IBuffEffect
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

        public void OnApply(BuffContainer container, BuffInstance instance)
        {
            // 属性修改通过 BuffModifier 在 GetModifiedValue 时计算
            // 这里可以触发属性变化事件
        }

        public void OnRemove(BuffContainer container, BuffInstance instance)
        {
            // 属性修改自动失效
            // 这里可以触发属性变化事件
        }

        public void OnStackChanged(BuffContainer container, BuffInstance instance, int oldStack, int newStack)
        {
            // 堆叠变化时属性修改值会自动调整（在 GetModifiedValue 中计算）
        }
    }
}
