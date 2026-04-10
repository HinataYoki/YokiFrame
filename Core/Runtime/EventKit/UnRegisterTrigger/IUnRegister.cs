using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Common unregister token contract used by EventKit.
    /// </summary>
    public interface IUnRegister
    {
        /// <summary>
        /// Removes the associated listener or callback.
        /// </summary>
        void UnRegister();
    }

    /// <summary>
    /// Delegate-backed unregister token.
    /// </summary>
    public struct CustomUnRegister : IUnRegister
    {
        private Action mUnRegisterAction;

        /// <summary>
        /// Creates an unregister token backed by a custom delegate.
        /// </summary>
        public CustomUnRegister(Action unRegister) => mUnRegisterAction = unRegister;

        /// <summary>
        /// Invokes the stored unregister delegate once.
        /// </summary>
        public void UnRegister()
        {
            mUnRegisterAction?.Invoke();
            mUnRegisterAction = null;
        }
    }

    /// <summary>
    /// Unregister token for parameterless <see cref="EasyEvent"/> listeners.
    /// </summary>
    public struct LinkUnRegister : IUnRegister
    {
        private PooledLinkedList<Action> mEventList;
        private LinkedListNode<Action> mNode;

        /// <summary>
        /// Creates an unregister token bound to one linked-list node.
        /// </summary>
        public LinkUnRegister(PooledLinkedList<Action> eventList, LinkedListNode<Action> node)
        {
            mEventList = eventList;
            mNode = node;
        }

        /// <summary>
        /// Removes the bound listener node and clears the token state.
        /// </summary>
        public void UnRegister()
        {
            mEventList.Remove(mNode);
            mEventList = null;
            mNode = null;
        }
    }

    /// <summary>
    /// Unregister token for payload-based <see cref="EasyEvent{T}"/> listeners.
    /// </summary>
    public struct LinkUnRegister<T> : IUnRegister
    {
        private PooledLinkedList<Action<T>> mEventList;
        private LinkedListNode<Action<T>> mNode;

        /// <summary>
        /// Creates an unregister token bound to one linked-list node.
        /// </summary>
        public LinkUnRegister(PooledLinkedList<Action<T>> eventList, LinkedListNode<Action<T>> node)
        {
            mEventList = eventList;
            mNode = node;
        }

        /// <summary>
        /// Removes the bound listener node and clears the token state.
        /// </summary>
        public void UnRegister()
        {
            mEventList.Remove(mNode);
            mEventList = null;
            mNode = null;
        }
    }
}
