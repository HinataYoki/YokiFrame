using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 基类，提供 IBuff 接口的默认实现
    /// </summary>
    public abstract class BaseBuff : IBuff
    {
        public virtual int BuffId { get; protected set; }
        public virtual float Duration { get; protected set; } = -1f;
        public virtual float TickInterval { get; protected set; } = 0f;
        public virtual int MaxStack { get; protected set; } = 1;
        public virtual StackMode StackMode { get; protected set; } = StackMode.Refresh;
        public virtual int[] Tags { get; protected set; }
        public virtual int[] ExclusionTags { get; protected set; }
        
        public IReadOnlyList<IBuffEffect> Effects => mEffects;
        public IReadOnlyList<BuffModifier> Modifiers => mModifiers;

        protected readonly List<IBuffEffect> mEffects = new();
        protected readonly List<BuffModifier> mModifiers = new();

        protected BaseBuff() { }

        protected BaseBuff(int buffId)
        {
            BuffId = buffId;
        }

        /// <summary>
        /// 从 BuffData 初始化
        /// </summary>
        public virtual void InitFromData(BuffData data)
        {
            BuffId = data.BuffId;
            Duration = data.Duration;
            MaxStack = data.MaxStack;
            StackMode = data.StackMode;
            TickInterval = data.TickInterval;
            Tags = data.Tags;
            ExclusionTags = data.ExclusionTags;
        }

        /// <summary>
        /// 添加效果
        /// </summary>
        public BaseBuff AddEffect(IBuffEffect effect)
        {
            mEffects.Add(effect);
            return this;
        }

        /// <summary>
        /// 添加属性修改器
        /// </summary>
        public BaseBuff AddModifier(BuffModifier modifier)
        {
            mModifiers.Add(modifier);
            return this;
        }

        /// <summary>
        /// 添加属性修改器
        /// </summary>
        public BaseBuff AddModifier(int attributeId, ModifierType type, float value, int priority = 0)
        {
            mModifiers.Add(new BuffModifier(attributeId, type, value, priority));
            return this;
        }

        public virtual void OnAdd(BuffContainer container, BuffInstance instance) { }
        public virtual void OnRemove(BuffContainer container, BuffInstance instance) { }
        public virtual void OnTick(BuffContainer container, BuffInstance instance) { }
        public virtual void OnExpire(BuffContainer container, BuffInstance instance) { }
        public virtual void OnStackChanged(BuffContainer container, BuffInstance instance, int oldStack, int newStack) { }
    }
}
