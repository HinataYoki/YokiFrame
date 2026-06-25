using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 使用的通用注销令牌契约。
    /// </summary>
    public interface IUnRegister
    {
        /// <summary>移除关联的监听器或回调。</summary>
        void UnRegister();
    }

    /// <summary>
    /// 基于委托的注销令牌。
    /// </summary>
    public struct CustomUnRegister : IUnRegister
    {
        private Action mUnRegisterAction;

        /// <summary>创建由自定义委托驱动的注销令牌。</summary>
        public CustomUnRegister(Action unRegister) => mUnRegisterAction = unRegister;

        /// <summary>调用一次已保存的注销委托。</summary>
        public void UnRegister()
        {
            mUnRegisterAction?.Invoke();
            mUnRegisterAction = null;
        }
    }

    /// <summary>
    /// 无参数 EasyEvent 监听器的注销令牌。
    /// </summary>
    public struct LinkUnRegister : IUnRegister
    {
        private EasyEvent mOwner;
        private PooledLinkedList<Action> mEventList;
        private LinkedListNode<Action> mNode;

        public LinkUnRegister(PooledLinkedList<Action> eventList, LinkedListNode<Action> node)
        {
            mOwner = null;
            mEventList = eventList;
            mNode = node;
        }

        internal LinkUnRegister(EasyEvent owner, LinkedListNode<Action> node)
        {
            mOwner = owner;
            mEventList = null;
            mNode = node;
        }

        public void UnRegister()
        {
            if (mNode == null)
                return;

            if (mOwner != null)
                mOwner.UnRegisterNode(mNode);
            else if (mEventList != null)
                mEventList.Remove(mNode);

            mOwner = null;
            mEventList = null;
            mNode = null;
        }
    }

    /// <summary>
    /// 带参数 EasyEvent&lt;T&gt; 监听器的注销令牌。
    /// </summary>
    public struct LinkUnRegister<T> : IUnRegister
    {
        private EasyEvent<T> mOwner;
        private PooledLinkedList<Action<T>> mEventList;
        private LinkedListNode<Action<T>> mNode;

        public LinkUnRegister(PooledLinkedList<Action<T>> eventList, LinkedListNode<Action<T>> node)
        {
            mOwner = null;
            mEventList = eventList;
            mNode = node;
        }

        internal LinkUnRegister(EasyEvent<T> owner, LinkedListNode<Action<T>> node)
        {
            mOwner = owner;
            mEventList = null;
            mNode = node;
        }

        public void UnRegister()
        {
            if (mNode == null)
                return;

            if (mOwner != null)
                mOwner.UnRegisterNode(mNode);
            else if (mEventList != null)
                mEventList.Remove(mNode);

            mOwner = null;
            mEventList = null;
            mNode = null;
        }
    }
}
