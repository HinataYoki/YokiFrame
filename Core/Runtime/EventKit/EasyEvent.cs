using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 无参数运行时事件的 EasyEvent 容器。
    /// </summary>
    public class EasyEvent : IEasyEvent
    {
        private readonly PooledLinkedList<Action> mEventList = new();
        private List<LinkedListNode<Action>> mPendingRemoveNodes;
        private int mTriggerDepth;

        /// <summary>注册监听器，并返回轻量注销令牌。</summary>
        public LinkUnRegister Register(Action action)
        {
            var node = mEventList.AddLast(action);
            return new LinkUnRegister(this, node);
        }

        /// <summary>注销一个匹配的监听器。</summary>
        public void UnRegister(Action action)
        {
            var node = mEventList.Last;
            while (node != null)
            {
                if (node.Value == action)
                {
                    UnRegisterNode(node);
                    break;
                }
                node = node.Previous;
            }
        }

        /// <summary>
        /// 按注册顺序调用所有监听器。
        /// 每次调用前会缓存下一个节点，因此监听器可以安全地注销自身。
        /// </summary>
        public void Trigger()
        {
            mTriggerDepth++;
            try
            {
                var node = mEventList.First;
                while (node != null)
                {
                    var current = node.Value;
                    var nxt = node.Next;
                    if (current != null)
                    {
                        try
                        {
                            current.Invoke();
                        }
                        catch (Exception e)
                        {
                            EventKitErrorHandler.OnError?.Invoke(
                                $"[EasyEvent] Class {current.Method?.DeclaringType} Method {current.Method?.Name} Error: {e.Message}\n{e.StackTrace}");
                        }
                    }
                    node = nxt;
                }
            }
            finally
            {
                mTriggerDepth--;
                if (mTriggerDepth == 0)
                    FlushPendingRemoveNodes();
            }
        }

        /// <summary>移除全部监听器。</summary>
        public void UnRegisterAll()
        {
            mEventList.Clear();
            mPendingRemoveNodes?.Clear();
        }

        /// <summary>当前监听器数量。</summary>
        public int ListenerCount => mEventList.Count;

        /// <summary>枚举全部已注册委托。</summary>
        public IEnumerable<Delegate> GetListeners()
        {
            var node = mEventList.First;
            while (node != null)
            {
                yield return node.Value;
                node = node.Next;
            }
        }

        internal void UnRegisterNode(LinkedListNode<Action> node)
        {
            if (node == null || node.List == null)
                return;

            if (mTriggerDepth > 0)
            {
                node.Value = null;
                if (mPendingRemoveNodes == null)
                    mPendingRemoveNodes = new List<LinkedListNode<Action>>();
                mPendingRemoveNodes.Add(node);
                return;
            }

            mEventList.Remove(node);
        }

        private void FlushPendingRemoveNodes()
        {
            if (mPendingRemoveNodes == null || mPendingRemoveNodes.Count == 0)
                return;

            for (int i = 0; i < mPendingRemoveNodes.Count; i++)
            {
                var node = mPendingRemoveNodes[i];
                if (node.List != null)
                    mEventList.Remove(node);
            }
            mPendingRemoveNodes.Clear();
        }
    }
}
