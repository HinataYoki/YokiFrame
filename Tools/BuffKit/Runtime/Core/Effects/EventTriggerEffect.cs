using System;

namespace YokiFrame
{
    /// <summary>
    /// 事件触发效果基类：封装事件订阅/取消订阅样板。
    /// 使用方通过构造函数注入订阅/反订阅委托，适配任意事件系统（EventKit/UniRx/C# event/MessagePipe 等）。
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public abstract class EventTriggerEffect<TEvent> : BuffEffectBase
    {
        private readonly Action<Action<TEvent>> mSubscribe;
        private readonly Action<Action<TEvent>> mUnsubscribe;
        private Action<TEvent> mHandler;
        private BuffContainer mContainer;
        private BuffInstance mInstance;

        protected EventTriggerEffect(
            Action<Action<TEvent>> subscribe,
            Action<Action<TEvent>> unsubscribe)
        {
            mSubscribe = subscribe;
            mUnsubscribe = unsubscribe;
        }

        public sealed override void OnApply(BuffContainer container, BuffInstance instance)
        {
            mContainer = container;
            mInstance = instance;
            mHandler = OnEventInternal;
            mSubscribe(mHandler);
            OnAppliedExtra(container, instance);
        }

        public sealed override void OnRemove(BuffContainer container, BuffInstance instance)
        {
            if (mHandler != null)
            {
                mUnsubscribe(mHandler);
                mHandler = null;
            }
            OnRemovedExtra(container, instance);
            mContainer = null;
            mInstance = null;
        }

        private void OnEventInternal(TEvent evt)
        {
            if (mContainer == null || mInstance == null) return;
            if (!ShouldReact(mContainer, mInstance, evt)) return;
            React(mContainer, mInstance, evt);
        }

        /// <summary>
        /// 是否响应该事件（默认始终响应）
        /// </summary>
        protected virtual bool ShouldReact(BuffContainer container, BuffInstance instance, TEvent evt) => true;

        /// <summary>
        /// 事件触发时的响应逻辑
        /// </summary>
        protected abstract void React(BuffContainer container, BuffInstance instance, TEvent evt);

        /// <summary>
        /// OnApply 扩展点（订阅之后调用）
        /// </summary>
        protected virtual void OnAppliedExtra(BuffContainer container, BuffInstance instance) { }

        /// <summary>
        /// OnRemove 扩展点（取消订阅之后调用）
        /// </summary>
        protected virtual void OnRemovedExtra(BuffContainer container, BuffInstance instance) { }
    }
}
