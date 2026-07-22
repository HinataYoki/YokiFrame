using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 带单个类型负载的事件容器，负责保存监听器并按注册顺序派发。
    /// </summary>
    /// <typeparam name="T">事件负载类型。</typeparam>
    public sealed class EasyEvent<T> : IEasyEvent
    {
        private readonly PooledLinkedList<Action<T>> mEventList = new PooledLinkedList<Action<T>>();
        private List<PooledLinkedListNode<Action<T>>> mPendingRemoveNodes;
        private int mTriggerDepth;

        /// <summary>
        /// 注册一个带负载监听器，并返回可手动或生命周期自动注销的令牌。
        /// </summary>
        /// <param name="action">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister<T> Register(Action<T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            PooledLinkedListNode<Action<T>> node = mEventList.AddLast(action);
            return new LinkUnRegister<T>(this, node);
        }

        /// <summary>
        /// 注销最后一个匹配的带负载监听器。
        /// </summary>
        /// <param name="action">需要移除的监听器。</param>
        /// <returns>实际找到并接收移除监听器时返回 true。</returns>
        public bool UnRegister(Action<T> action)
        {
            if (action == null)
            {
                return false;
            }

            PooledLinkedListNode<Action<T>> node = mEventList.Last;
            while (node.IsValid)
            {
                if (node.Value == action)
                {
                    return UnRegisterNode(node);
                }

                node = node.Previous;
            }

            return false;
        }

        /// <summary>
        /// 使用指定负载按注册顺序触发全部监听器。
        /// </summary>
        /// <param name="args">事件负载。</param>
        public void Trigger(T args)
        {
            mTriggerDepth++;
            try
            {
                TriggerListeners(args);
            }
            finally
            {
                mTriggerDepth--;
                if (mTriggerDepth == 0)
                {
                    FlushPendingRemoveNodes();
                }
            }
        }

        /// <summary>
        /// 移除当前容器中的全部监听器，并清理派发中延迟移除队列。
        /// </summary>
        public void UnRegisterAll()
        {
            mEventList.Clear();
            mPendingRemoveNodes?.Clear();
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 获取当前容器中仍被保存的监听器数量。
        /// </summary>
        public int ListenerCount
        {
            get { return mEventList.Count; }
        }

        /// <summary>
        /// 枚举当前已注册监听器，跳过派发中已标记待移除的空节点。
        /// </summary>
        /// <returns>当前容器内仍有效的监听器。</returns>
        public IEnumerable<Delegate> GetListeners()
        {
            PooledLinkedListNode<Action<T>> node = mEventList.First;
            while (node.IsValid)
            {
                if (node.Value != null)
                {
                    yield return node.Value;
                }

                node = node.Next;
            }
        }
#endif

        /// <summary>
        /// 按链表顺序派发监听器，并捕获单个监听器异常避免中断后续监听器。
        /// </summary>
        /// <param name="args">事件负载。</param>
        private void TriggerListeners(T args)
        {
            PooledLinkedListNode<Action<T>> node = mEventList.First;
            while (node.IsValid)
            {
                Action<T> current = node.Value;
                PooledLinkedListNode<Action<T>> next = node.Next;
                if (current != null)
                {
                    InvokeListener(current, args);
                }

                node = next;
            }
        }

        /// <summary>
        /// 调用单个监听器，并把异常交给宿主或 LogKit 处理。
        /// </summary>
        /// <param name="listener">要调用的监听器。</param>
        /// <param name="args">事件负载。</param>
        private static void InvokeListener(Action<T> listener, T args)
        {
            try
            {
                listener.Invoke(args);
            }
            catch (Exception exception)
            {
                EventKitErrorHandler.Report(BuildErrorMessage(listener, exception));
            }
        }

        /// <summary>
        /// 构造包含监听器来源和异常栈的诊断信息。
        /// </summary>
        /// <param name="listener">抛出异常的监听器。</param>
        /// <param name="exception">监听器抛出的异常。</param>
        /// <returns>格式化后的错误信息。</returns>
        private static string BuildErrorMessage(Action<T> listener, Exception exception)
        {
            return "[EasyEvent<" + typeof(T).Name + ">] Class " + listener.Method.DeclaringType
                + " Method " + listener.Method.Name
                + " Error: " + exception.Message + "\n" + exception.StackTrace;
        }

        /// <summary>
        /// 注销指定链表节点；派发中调用时先置空，待最外层派发结束后统一移除。
        /// </summary>
        /// <param name="node">需要移除的监听器节点。</param>
        /// <returns>节点由当前容器实际接收移除时返回 true。</returns>
        internal bool UnRegisterNode(PooledLinkedListNode<Action<T>> node)
        {
            if (!mEventList.IsNodeLeaseValid(node) || node.Value == null)
            {
                return false;
            }

            if (mTriggerDepth > 0)
            {
                MarkNodeForLaterRemoval(node);
                return true;
            }

            return mEventList.Remove(node);
        }

        /// <summary>
        /// 标记派发中的节点，避免当前链表枚举被立即修改。
        /// </summary>
        /// <param name="node">需要延迟移除的监听器节点。</param>
        private void MarkNodeForLaterRemoval(PooledLinkedListNode<Action<T>> node)
        {
            node.Value = null;
            if (mPendingRemoveNodes == null)
            {
                mPendingRemoveNodes = new List<PooledLinkedListNode<Action<T>>>();
            }

            mPendingRemoveNodes.Add(node);
        }

        /// <summary>
        /// 派发结束后移除全部已标记节点。
        /// </summary>
        private void FlushPendingRemoveNodes()
        {
            if (mPendingRemoveNodes == null || mPendingRemoveNodes.Count == 0)
            {
                return;
            }

            for (var index = 0; index < mPendingRemoveNodes.Count; index++)
            {
                mEventList.Remove(mPendingRemoveNodes[index]);
            }

            mPendingRemoveNodes.Clear();
        }
    }
}
