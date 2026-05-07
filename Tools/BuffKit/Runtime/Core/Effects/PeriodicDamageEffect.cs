using System;

namespace YokiFrame
{
    /// <summary>
    /// 周期伤害效果 - 在 Buff Tick 时自动造成伤害
    /// </summary>
    public class PeriodicDamageEffect : BuffEffectBase
    {
        private readonly float mDamagePerTick;
        private readonly Action<BuffContainer, BuffInstance, float> mOnDamage;

        /// <summary>
        /// 创建周期伤害效果
        /// </summary>
        /// <param name="damagePerTick">每次 Tick 的基础伤害值</param>
        /// <param name="onDamage">伤害回调（接收 container/instance/实际伤害值）</param>
        public PeriodicDamageEffect(float damagePerTick, Action<BuffContainer, BuffInstance, float> onDamage = null)
        {
            mDamagePerTick = damagePerTick;
            mOnDamage = onDamage;
        }

        public override void OnTick(BuffContainer container, BuffInstance instance)
        {
            var damage = CalculateDamage(instance);
            mOnDamage?.Invoke(container, instance, damage);
        }

        /// <summary>
        /// 计算当前伤害值（考虑堆叠）
        /// </summary>
        public float CalculateDamage(BuffInstance instance)
        {
            return mDamagePerTick * instance.StackCount;
        }
    }
}
