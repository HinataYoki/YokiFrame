using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// EasyEvent container for payload-based runtime events.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    public class EasyEvent<T> : IEasyEvent
    {
        private readonly PooledLinkedList<Action<T>> mEventList = new();

        /// <summary>
        /// Registers a listener and returns a lightweight unregister token.
        /// </summary>
        public LinkUnRegister<T> Register(Action<T> action)
        {
            var node = mEventList.AddLast(action);
            return new LinkUnRegister<T>(mEventList, node);
        }

        /// <summary>
        /// Unregisters one matching listener.
        /// </summary>
        public void UnRegister(Action<T> action)
        {
            var node = mEventList.Last;
            while (node != null)
            {
                if (node.Value == action)
                {
                    mEventList.Remove(node);
                    break;
                }

                node = node.Previous;
            }
        }

        /// <summary>
        /// Invokes all listeners with the supplied payload.
        /// </summary>
        /// <remarks>
        /// The next node is cached before each invocation so listeners can safely unregister themselves while the
        /// event is being dispatched.
        /// </remarks>
        public void Trigger(T args)
        {
            var node = mEventList.First;
            while (node != null)
            {
                var nxt = node.Next;
                try
                {
                    node.Value?.Invoke(args);
                }
                catch (Exception e)
                {
                    KitLogger.Error($"[EasyEvent<{typeof(T).Name}>] 类 {node.Value?.Method?.DeclaringType} 方法 {node.Value?.Method?.Name} 执行出错: {e.Message}\n{e.StackTrace}");
                }

                node = nxt;
            }
        }

        /// <summary>
        /// Removes all listeners.
        /// </summary>
        public void UnRegisterAll() => mEventList.Clear();

        /// <summary>
        /// Current listener count.
        /// </summary>
        public int ListenerCount => mEventList.Count;

        /// <summary>
        /// Enumerates all registered delegates.
        /// </summary>
        public IEnumerable<Delegate> GetListeners()
        {
            var node = mEventList.First;
            while (node != null)
            {
                yield return node.Value;
                node = node.Next;
            }
        }
    }
}
