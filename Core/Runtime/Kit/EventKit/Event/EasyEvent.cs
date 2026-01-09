using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 无参事件
    /// </summary>
    public class EasyEvent : IEasyEvent
    {
        private readonly PooledLinkedList<Action> mEventList = new();

        public LinkUnRegister Register(Action action)
        {
            var node = mEventList.AddLast(action);
            return new LinkUnRegister(mEventList, node);
        }

        public void UnRegister(Action action)
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

        public void Trigger()
        {
            var node = mEventList.First;
            while (node != null)
            {
                var nxt = node.Next;
                try
                {
                    node.Value?.Invoke();
                }
                catch (Exception e)
                {
                    KitLogger.Error($"[EasyEvent] 类 {node.Value?.Method?.DeclaringType} 方法 {node.Value?.Method?.Name} 执行出错: {e.Message}\n{e.StackTrace}");
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
