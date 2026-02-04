namespace YokiFrame.ECS
{
    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType : byte
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        True // 真实伤害，无视防御
    }
    
    /// <summary>
    /// 伤害组件 - 用于造成伤害的实体
    /// </summary>
    public struct Damage : IComponentData
    {
        public float Amount;
        public DamageType Type;
        public long SourceEntityId;
        public bool DestroyOnHit;
        
        public Damage(float amount, DamageType type = DamageType.Physical, bool destroyOnHit = true)
        {
            Amount = amount;
            Type = type;
            SourceEntityId = -1;
            DestroyOnHit = destroyOnHit;
        }
        
        public Damage(float amount, long sourceEntityId, DamageType type = DamageType.Physical, bool destroyOnHit = true)
        {
            Amount = amount;
            Type = type;
            SourceEntityId = sourceEntityId;
            DestroyOnHit = destroyOnHit;
        }
    }
}
