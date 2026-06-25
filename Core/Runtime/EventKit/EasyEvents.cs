using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 小型类型事件容器，按事件类型保存多个 IEasyEvent 实例。
    /// </summary>
    public class EasyEvents
    {
        private readonly Dictionary<Type, IEasyEvent> mTypeEventDic = new();

        /// <summary>添加指定类型的空事件容器。</summary>
        public void AddEvent<T>() where T : IEasyEvent, new() => mTypeEventDic.Add(typeof(T), new T());

        /// <summary>获取此前添加的事件容器。</summary>
        public T GetEvent<T>() where T : IEasyEvent
        {
            return mTypeEventDic.TryGetValue(typeof(T), out var typeEvent) ? (T)typeEvent : default;
        }

        /// <summary>获取事件容器；首次访问时自动创建。</summary>
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

        /// <summary>移除全部缓存的事件容器。</summary>
        public void Clear()
        {
            foreach (var kvp in mTypeEventDic)
            {
                kvp.Value.UnRegisterAll();
            }
            mTypeEventDic.Clear();
        }

        /// <summary>返回全部已注册事件容器，用于诊断或编辑器检查。</summary>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents() => mTypeEventDic;
    }
}
