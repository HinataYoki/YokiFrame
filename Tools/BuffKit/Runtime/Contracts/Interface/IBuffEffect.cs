namespace YokiFrame
{
    /// <summary>
    /// Buff 效果接口，定义 Buff 产生的具体效果
    /// </summary>
    public interface IBuffEffect
    {
        /// <summary>
        /// 效果应用时回调
        /// </summary>
        void OnApply(BuffContainer container, BuffInstance instance);

        /// <summary>
        /// 效果移除时回调
        /// </summary>
        void OnRemove(BuffContainer container, BuffInstance instance);

        /// <summary>
        /// 堆叠层数变化时回调（默认空实现）
        /// </summary>
        void OnStackChanged(BuffContainer container, BuffInstance instance, int oldStack, int newStack) { }

        /// <summary>
        /// 周期触发回调（默认空实现，由 BuffContainer.Update 根据 TickInterval 触发）
        /// </summary>
        void OnTick(BuffContainer container, BuffInstance instance) { }
    }
}
