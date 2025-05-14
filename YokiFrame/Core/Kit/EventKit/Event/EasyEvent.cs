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
        private readonly PooledLinkedList<Action> Events = new();

        public CustomUnRegister Register(Action action)
        {
            var node = Events.AddLast(action);
            return new CustomUnRegister(() => Events.Remove(node));
        }

        public void UnRegister(Action action)
        {
            var node = Events.Last;
            while (node != null)
            {
                if (node.Value == action)
                {
                    Events.Remove(node);
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
            var node = Events.First;
            while (node != null)
            {
                var nxt = node.Next;
                try
                {
                    node.Value?.Invoke();
                }
                catch
                {
                    LogKit.Error<EasyEvent>($"类 {node.Value.Method.DeclaringType} 方法 {node.Value.Method} 出错");
                }
                node = nxt;
            }
        }

        public void UnRegisterAll() => Events.Clear();
    }

    public class EasyEvent<T> : IEasyEvent
    {
        private readonly PooledLinkedList<Action<T>> Events = new();

        public CustomUnRegister Register(Action<T> action)
        {
            var node = Events.AddLast(action);
            return new CustomUnRegister(() => { Events.Remove(node); });
        }

        public void UnRegister(Action<T> action)
        {
            var node = Events.Last;
            while (node != null)
            {
                if (node.Value == action)
                {
                    Events.Remove(node);
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
            var node = Events.First;
            while (node != null)
            {
                var nxt = node.Next;
                try
                {
                    node.Value?.Invoke(args);
                }
                catch
                {
                    LogKit.Error<EasyEvent>($"类 {node.Value.Method.DeclaringType} 方法 {node.Value.Method} 出错");
                }
                node = nxt;
            }
        }

        public void UnRegisterAll() => Events.Clear();
    }

    public class EasyEvents
    {
        private readonly Dictionary<Type, IEasyEvent> mTypeEvents = new();

        public void AddEvent<T>() where T : IEasyEvent, new() => mTypeEvents.Add(typeof(T), new T());

        public T GetEvent<T>() where T : IEasyEvent
        {
            return mTypeEvents.TryGetValue(typeof(T), out var typeEvent) ? (T)typeEvent : default;
        }

        public T GetOrAddEvent<T>() where T : IEasyEvent, new()
        {
            var type = typeof(T);
            if (!mTypeEvents.TryGetValue(type, out var typeEvent))
            {
                typeEvent = new T();
                mTypeEvents.Add(type, typeEvent);
            }

            return (T)typeEvent;
        }

        public void Clear() => mTypeEvents.Clear();
    }
}