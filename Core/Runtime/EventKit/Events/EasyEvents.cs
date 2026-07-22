using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 按事件容器类型保存 EasyEvent 实例的小型集合。
    /// </summary>
    public sealed class EasyEvents
    {
        private readonly Dictionary<Type, IEasyEvent> mTypeEventDic = new Dictionary<Type, IEasyEvent>();

        /// <summary>
        /// 获取此前添加的事件容器；不存在时返回 null。
        /// </summary>
        /// <typeparam name="T">要获取的事件容器类型。</typeparam>
        /// <returns>已存在的事件容器。</returns>
        public T GetEvent<T>() where T : class, IEasyEvent
        {
            IEasyEvent typeEvent;
            return mTypeEventDic.TryGetValue(typeof(T), out typeEvent) ? typeEvent as T : null;
        }

        /// <summary>
        /// 获取事件容器；首次访问时自动创建。
        /// </summary>
        /// <typeparam name="T">要获取或创建的事件容器类型。</typeparam>
        /// <returns>事件容器实例。</returns>
        public T GetOrAddEvent<T>() where T : class, IEasyEvent, new()
        {
            Type type = typeof(T);
            IEasyEvent typeEvent;
            if (!mTypeEventDic.TryGetValue(type, out typeEvent))
            {
                typeEvent = new T();
                mTypeEventDic.Add(type, typeEvent);
            }

            return (T)typeEvent;
        }

        /// <summary>
        /// 移除全部缓存的事件容器，并先清理每个容器内部监听器。
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<Type, IEasyEvent> pair in mTypeEventDic)
            {
                pair.Value.UnRegisterAll();
            }

            mTypeEventDic.Clear();
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 返回全部已注册事件容器，用于编辑器检查或诊断。
        /// </summary>
        /// <returns>当前保存的事件容器字典。</returns>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents()
        {
            return mTypeEventDic;
        }
#endif
    }
}
