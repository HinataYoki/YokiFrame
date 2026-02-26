using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public class TypeEvent
    {
        private readonly EasyEvents mEventDic = new();

        /// <summary>
        /// 触发方法
        /// </summary>
        public void Send<T>(T args = default)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnSend?.Invoke("Type", typeof(T).Name, args);
#endif
            mEventDic.GetEvent<EasyEvent<T>>()?.Trigger(args);
        }
        /// <summary>
        /// 注册方法
        /// </summary>
        /// <returns></returns>
        public LinkUnRegister<T> Register<T>(Action<T> onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnRegister?.Invoke(onEvent);
#endif
            return mEventDic.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        }
        /// <summary>
        /// 注销方法
        /// </summary>
        public void UnRegister<T>(Action<T> onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnUnRegister?.Invoke(onEvent);
#endif
            mEventDic.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);
        }

        public void Clear() => mEventDic.Clear();
        
        /// <summary>
        /// 获取所有已注册的类型事件（用于编辑器可视化）
        /// </summary>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents() => mEventDic.GetAllEvents();
    }
}