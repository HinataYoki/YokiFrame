using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 泛型事件（带参数）
    /// </summary>
    public class EasyEvent<T> : IEasyEvent
    {
        private readonly PooledLinkedList<Action<T>> mEventList = new();

        public LinkUnRegister<T> Register(Action<T> action)
        {
            var node = mEventList.AddLast(action);
            return new LinkUnRegister<T>(mEventList, node);
        }

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

        public void UnRegisterAll() => mEventList.Clear();
        public int ListenerCount => mEventList.Count;

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
