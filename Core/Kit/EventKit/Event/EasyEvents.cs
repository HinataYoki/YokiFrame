using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 事件容器（按类型存储多个事件）
    /// </summary>
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
        
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents() => mTypeEventDic;
    }
}
