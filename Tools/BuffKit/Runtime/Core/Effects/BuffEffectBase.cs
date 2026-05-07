namespace YokiFrame
{
    /// <summary>
    /// IBuffEffect 便捷抽象基类，所有回调默认空实现。
    /// 自定义 effect 继承此类可只实现需要的回调。
    /// </summary>
    public abstract class BuffEffectBase : IBuffEffect
    {
        public virtual void OnApply(BuffContainer container, BuffInstance instance) { }
        public virtual void OnRemove(BuffContainer container, BuffInstance instance) { }
        public virtual void OnStackChanged(BuffContainer container, BuffInstance instance, int oldStack, int newStack) { }
        public virtual void OnTick(BuffContainer container, BuffInstance instance) { }
    }
}
