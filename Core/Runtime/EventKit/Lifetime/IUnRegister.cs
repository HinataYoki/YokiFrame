using System;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 使用的通用注销令牌契约。
    /// </summary>
    public interface IUnRegister
    {
        /// <summary>
        /// 移除令牌关联的监听器或回调。
        /// </summary>
        void UnRegister();
    }

    /// <summary>
    /// 基于自定义委托的注销令牌，用于适配非 EventKit 的释放逻辑。
    /// </summary>
    public struct CustomUnRegister : IUnRegister
    {
        private Action mUnRegisterAction;

        /// <summary>
        /// 创建由指定委托驱动的注销令牌。
        /// </summary>
        /// <param name="unRegister">执行注销时调用的委托。</param>
        public CustomUnRegister(Action unRegister)
        {
            mUnRegisterAction = unRegister;
        }

        /// <summary>
        /// 调用一次已保存的注销委托，并清空自身持有的委托引用。
        /// </summary>
        public void UnRegister()
        {
            Action action = mUnRegisterAction;
            mUnRegisterAction = null;
            action?.Invoke();
        }
    }

    /// <summary>
    /// 无参数 EasyEvent 监听器的注销令牌。
    /// </summary>
    public struct LinkUnRegister : IUnRegister
    {
        private EasyEvent mOwner;
        private PooledLinkedListNode<Action> mNode;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private EventKitEditorNotification mUnregisterNotification;
        private bool mHasUnregisterNotification;
#endif

        /// <summary>
        /// 创建绑定到指定 EasyEvent 节点的注销令牌。
        /// </summary>
        /// <param name="owner">拥有该监听器节点的事件容器。</param>
        /// <param name="node">需要在注销时移除的链表节点。</param>
        internal LinkUnRegister(EasyEvent owner, PooledLinkedListNode<Action> node)
        {
            mOwner = owner;
            mNode = node;
#if UNITY_EDITOR || (GODOT && TOOLS)
            mUnregisterNotification = default;
            mHasUnregisterNotification = false;
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 绑定监听器实际移除后的无闭包诊断通知，供 EventKit hook 保持版本准确。
        /// </summary>
        /// <param name="notification">已包含通道、事件键和监听器身份的注销通知。</param>
        /// <returns>绑定通知后的令牌副本。</returns>
        internal LinkUnRegister WithEditorUnregisterNotification(EventKitEditorNotification notification)
        {
            mUnregisterNotification = notification;
            mHasUnregisterNotification = true;
            return this;
        }
#endif

        /// <summary>
        /// 移除关联监听器；如果节点已被移除则静默跳过。
        /// </summary>
        public void UnRegister()
        {
            if (mOwner == null || !mNode.IsValid)
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            bool removed = mOwner.UnRegisterNode(mNode);
#else
            mOwner.UnRegisterNode(mNode);
#endif
            mOwner = null;
            mNode = default;
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (removed && mHasUnregisterNotification)
            {
                EasyEventEditorHook.Publish(mUnregisterNotification);
            }

            mUnregisterNotification = default;
            mHasUnregisterNotification = false;
#endif
        }
    }

    /// <summary>
    /// 带参数 EasyEvent 监听器的注销令牌。
    /// </summary>
    /// <typeparam name="T">监听器接收的事件负载类型。</typeparam>
    public struct LinkUnRegister<T> : IUnRegister
    {
        private EasyEvent<T> mOwner;
        private PooledLinkedListNode<Action<T>> mNode;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private EventKitEditorNotification mUnregisterNotification;
        private bool mHasUnregisterNotification;
#endif

        /// <summary>
        /// 创建绑定到指定 EasyEvent 节点的注销令牌。
        /// </summary>
        /// <param name="owner">拥有该监听器节点的事件容器。</param>
        /// <param name="node">需要在注销时移除的链表节点。</param>
        internal LinkUnRegister(EasyEvent<T> owner, PooledLinkedListNode<Action<T>> node)
        {
            mOwner = owner;
            mNode = node;
#if UNITY_EDITOR || (GODOT && TOOLS)
            mUnregisterNotification = default;
            mHasUnregisterNotification = false;
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 绑定监听器实际移除后的无闭包诊断通知，供 EventKit hook 保持版本准确。
        /// </summary>
        /// <param name="notification">已包含通道、事件键和监听器身份的注销通知。</param>
        /// <returns>绑定通知后的令牌副本。</returns>
        internal LinkUnRegister<T> WithEditorUnregisterNotification(EventKitEditorNotification notification)
        {
            mUnregisterNotification = notification;
            mHasUnregisterNotification = true;
            return this;
        }
#endif

        /// <summary>
        /// 移除关联监听器；如果节点已被移除则静默跳过。
        /// </summary>
        public void UnRegister()
        {
            if (mOwner == null || !mNode.IsValid)
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            bool removed = mOwner.UnRegisterNode(mNode);
#else
            mOwner.UnRegisterNode(mNode);
#endif
            mOwner = null;
            mNode = default;
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (removed && mHasUnregisterNotification)
            {
                EasyEventEditorHook.Publish(mUnregisterNotification);
            }

            mUnregisterNotification = default;
            mHasUnregisterNotification = false;
#endif
        }
    }
}
