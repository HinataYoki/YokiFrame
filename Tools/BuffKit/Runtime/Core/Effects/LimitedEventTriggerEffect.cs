using System;

namespace YokiFrame
{
    /// <summary>
    /// 受限次数的事件触发效果：触发 N 次后自动将 buff 移除。
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public abstract class LimitedEventTriggerEffect<TEvent> : EventTriggerEffect<TEvent>
    {
        private readonly int mMaxTriggers;
        private int mTriggerCount;

        protected LimitedEventTriggerEffect(
            Action<Action<TEvent>> subscribe,
            Action<Action<TEvent>> unsubscribe,
            int maxTriggers) : base(subscribe, unsubscribe)
        {
            mMaxTriggers = maxTriggers;
        }

        /// <summary>
        /// 已触发次数
        /// </summary>
        public int TriggerCount => mTriggerCount;

        /// <summary>
        /// 最大触发次数
        /// </summary>
        public int MaxTriggers => mMaxTriggers;

        protected sealed override void React(BuffContainer container, BuffInstance instance, TEvent evt)
        {
            OnTriggered(container, instance, evt);
            mTriggerCount++;
            if (mTriggerCount >= mMaxTriggers)
            {
                container.RemoveInstance(instance);
            }
        }

        protected override void OnAppliedExtra(BuffContainer container, BuffInstance instance)
        {
            mTriggerCount = 0;
        }

        /// <summary>
        /// 事件触发时的响应逻辑（不用手动处理计数/移除）
        /// </summary>
        protected abstract void OnTriggered(BuffContainer container, BuffInstance instance, TEvent evt);
    }
}
