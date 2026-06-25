using System;
using System.Collections.Generic;
#if UNITY_EDITOR || GODOT
using System.Runtime.CompilerServices;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 以参数类型为键的运行时事件中心。
    /// </summary>
    public class TypeEvent
    {
        private readonly EasyEvents mEventDic = new();

        /// <summary>按参数类型发送事件。</summary>
        public void Send<T>(T args = default
#if UNITY_EDITOR || GODOT
            , [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = 0
#endif
        )
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnSend?.Invoke("Type", typeof(T).Name, args, sourceFile, sourceLine);
#endif
            mEventDic.GetEvent<EasyEvent<T>>()?.Trigger(args);
        }

        /// <summary>注册类型监听器。</summary>
        public LinkUnRegister<T> Register<T>(Action<T> onEvent)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnRegister?.Invoke("Type", typeof(T).Name, onEvent);
#endif
            return mEventDic.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        }

        /// <summary>注销一个类型监听器。</summary>
        public void UnRegister<T>(Action<T> onEvent)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnUnRegister?.Invoke("Type", typeof(T).Name, onEvent);
#endif
            mEventDic.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);
        }

        /// <summary>清空全部已注册类型事件。</summary>
        public void Clear()
        {
            mEventDic.Clear();
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnClear?.Invoke("Type", "*");
#endif
        }

        /// <summary>返回全部已注册类型事件，用于编辑器检查。</summary>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents() => mEventDic.GetAllEvents();
    }
}
