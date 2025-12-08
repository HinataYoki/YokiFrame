using System.Collections.Generic;
using System;

namespace YokiFrame
{
    public interface IEasyEvent
    {
        void UnRegisterAll();
    }

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
                else
                {
                    node = node.Previous;
                }
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
                catch
                {
                    UnityEngine.Debug.LogError($"类 {node.Value.Method.DeclaringType} 方法 {node.Value.Method} 出错");
                }
                node = nxt;
            }
        }

        public void UnRegisterAll() => mEventList.Clear();
    }

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
                else
                {
                    node = node.Previous;
                }
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
                catch
                {
                    KitLogger.Error($"类 {node.Value.Method.DeclaringType} 方法 {node.Value.Method} 出错");
                }
                node = nxt;
            }
        }

        public void UnRegisterAll() => mEventList.Clear();
    }

    public class EasyEvents
    {
        private readonly Dictionary<Type, IEasyEvent> mTypeEventDic = new();

        public void AddEvent<T>() where T : IEasyEvent, new() => mTypeEventDic.Add(typeof(T), new T());

        public T GetEvent<T>() where T : IEasyEvent
        {
            return mTypeEventDic.TryGetValue(typeof(T), out var typeEvent) ? (T)typeEvent : default;
        }

        public T GetOrAddEvent<T>() where T : IEasyEvent, new()
        {
            var type = typeof(T);
            if (!mTypeEventDic.TryGetValue(type, out var typeEvent))
            {
                typeEvent = new T();
                mTypeEventDic.Add(type, typeEvent);
            }

            return (T)typeEvent;
        }

        public void Clear() => mTypeEventDic.Clear();
    }
}