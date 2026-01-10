using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public interface IUnRegister
    {
        void UnRegister();
    }

    public struct CustomUnRegister : IUnRegister
    {
        private Action mUnRegisterAction;
        
        public CustomUnRegister(Action unRegister) => mUnRegisterAction = unRegister;

        public void UnRegister()
        {
            mUnRegisterAction?.Invoke();
            mUnRegisterAction = null;
        }
    }

    public struct LinkUnRegister : IUnRegister
    {
        private PooledLinkedList<Action> mEventList;
        private LinkedListNode<Action> mNode;

        public LinkUnRegister(PooledLinkedList<Action> eventList, LinkedListNode<Action> node)
        {
            mEventList = eventList;
            mNode = node;
        }

        public void UnRegister()
        {
            mEventList.Remove(mNode);
            mEventList = null;
            mNode = null;
        }
    }

    public struct LinkUnRegister<T> : IUnRegister
    {
        private PooledLinkedList<Action<T>> mEventList;
        private LinkedListNode<Action<T>> mNode;

        public LinkUnRegister(PooledLinkedList<Action<T>> eventList, LinkedListNode<Action<T>> node)
        {
            mEventList = eventList;
            mNode = node;
        }

        public void UnRegister()
        {
            mEventList.Remove(mNode);
            mEventList = null;
            mNode = null;
        }
    }
}
