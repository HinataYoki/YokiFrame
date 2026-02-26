using System.Collections.Generic;
using System;

namespace YokiFrame
{
    [Obsolete("StringEvent 存在类型安全隐患且重构困难，建议优先使用 TypeEvent 或 EnumEvent。")]
    public class StringEvent
    {
        private readonly Dictionary<string, EasyEvents> mEventDic = new();
        private void GetEvents(string key, out EasyEvents stringEvent)
        {
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                stringEvent = new();
                mEventDic.Add(key, stringEvent);
            }
        }
        /// <summary>
        /// 触发无参方法
        /// </summary>
        public void Send(string key)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnSend?.Invoke("String", key, null);
#endif
            GetEvents(key, out var stringEvent);
            stringEvent.GetEvent<EasyEvent>()?.Trigger();
        }
        /// <summary>
        /// 触发有参方法
        /// </summary>
        public void Send<T>(string key, T args)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnSend?.Invoke("String", key, args);
#endif
            GetEvents(key, out var stringEvent);
            stringEvent.GetEvent<EasyEvent<T>>()?.Trigger(args);
        }
        /// <summary>
        /// 触发可变参数方法
        /// </summary>
        public void Send(string key, params object[] args) => Send<object[]>(key, args);

        /// <summary>
        /// 注册无参方法
        /// </summary>
        public LinkUnRegister Register(string key, Action onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var stringEvent);
            return stringEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
        }
        /// <summary>
        /// 注册有参方法
        /// </summary>
        public LinkUnRegister<T> Register<T>(string key, Action<T> onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var stringEvent);
            return stringEvent.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        }
        /// <summary>
        /// 注册可变参数方法
        /// </summary>
        public LinkUnRegister<object[]> Register(string key, Action<object[]> onEvent) => Register<object[]>(key, onEvent);

        /// <summary>
        /// 注销此字符串所有方法
        /// </summary>
        public void UnRegister(string key)
        {
            GetEvents(key, out var stringEvent);
            stringEvent.Clear();
        }
        /// <summary>
        /// 注销无参方法
        /// </summary>
        public void UnRegister(string key, Action onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnUnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var stringEvent);
            stringEvent.GetEvent<EasyEvent>()?.UnRegister(onEvent);
        }
        /// <summary>
        /// 注销有参方法
        /// </summary>
        public void UnRegister<T>(string key, Action<T> onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnUnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var stringEvent);
            stringEvent.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);
        }
        /// <summary>
        /// 注销可变参数方法
        /// </summary>
        public void UnRegister(string key, Action<object[]> onEvent) => UnRegister<object[]>(key, onEvent);

        public void Clear() => mEventDic.Clear();
        
        /// <summary>
        /// 获取所有已注册的字符串事件（用于编辑器可视化）
        /// </summary>
        public IReadOnlyDictionary<string, EasyEvents> GetAllEvents() => mEventDic;
    }
}