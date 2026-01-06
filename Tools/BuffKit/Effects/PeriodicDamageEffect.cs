using System;

namespace YokiFrame
{
    /// <summary>
    /// 周期伤害效果示例 - 每次 Tick 时造成伤害
    /// </summary>
    public class PeriodicDamageEffect : IBuffEffect
    {
        private readonly float mDamagePerTick;
        private readonly Action<BuffContainer, BuffInstance, float> mOnDamage;

        /// <summary>
        /// 创建周期伤害效果
        /// </summary>
        /// <param name="damagePerTick">每次 Tick 的伤害值</param>
        /// <param name="onDamage">伤害回调（可选）</param>
        public PeriodicDamageEffect(float damagePerTick, Action<BuffContainer, BuffInstance, float> onDamage = null)
        {
            mDamagePerTick = damagePerTick;
            mOnDamage = onDamage;
        }

        public void OnApply(BuffContainer container, BuffInstance instance)
        {
            // 可以在这里初始化伤害相关数据
        }

        public void OnRemove(BuffContainer container, BuffInstance instance)
        {
            // 清理
        }

        public void OnStackChanged(BuffContainer container, BuffInstance instance, int oldStack, int newStack)
        {
            // 堆叠变化可能影响伤害值
        }

        /// <summary>
        /// 计算当前伤害值（考虑堆叠）
        /// </summary>
        public float CalculateDamage(BuffInstance instance)
        {
            return mDamagePerTick * instance.StackCount;
        }

        /// <summary>
        /// 执行伤害（由 Buff 的 OnTick 调用）
        /// </summary>
        public void ApplyDamage(BuffContainer container, BuffInstance instance)
        {
            float damage = CalculateDamage(instance);
            mOnDamage?.Invoke(container, instance, damage);
        }
    }
}
