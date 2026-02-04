using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 生命值组件
    /// </summary>
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;
        
        public Health(float max)
        {
            Max = max;
            Current = max;
        }
        
        public Health(float current, float max)
        {
            Current = current;
            Max = max;
        }
        
        public bool IsDead => Current <= 0;
        public bool IsAlive => Current > 0;
        public float Percent => Max > 0 ? Mathf.Clamp01(Current / Max) : 0;
        
        public void TakeDamage(float damage)
        {
            Current = Mathf.Max(0, Current - damage);
        }
        
        public void Heal(float amount)
        {
            Current = Mathf.Min(Max, Current + amount);
        }
        
        public void SetFull()
        {
            Current = Max;
        }
    }
}
