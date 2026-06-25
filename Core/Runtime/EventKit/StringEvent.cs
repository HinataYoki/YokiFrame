using System;
using System.Collections.Generic;
#if UNITY_EDITOR || GODOT
using System.Runtime.CompilerServices;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 以字符串为键的运行时事件中心。
    /// </summary>
    /// <remarks>
    /// 该 API 为兼容保留；新系统应优先使用 TypeEvent 或 EnumEvent，
    /// 因为它们更容易重构，也能提供更好的类型安全。
    /// </remarks>
    [Obsolete("StringEvent has type-safety issues. Prefer TypeEvent or EnumEvent instead.")]
    public class StringEvent
    {
        private readonly Dictionary<string, EasyEvents> mEventDic = new();

        private void GetEvents(string key, out EasyEvents stringEvent)
        {
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                stringEvent = new EasyEvents();
                mEventDic.Add(key, stringEvent);
            }
        }

        /// <summary>发送无参数字符串事件。</summary>
        public void Send(string key)
        {
            SendCore<object>(key, null, null, 0, false);
        }

        /// <summary>发送带类型参数的字符串事件。</summary>
        public void Send<T>(string key, T args
#if UNITY_EDITOR || GODOT
            , [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = 0
#endif
        )
        {
#if UNITY_EDITOR || GODOT
            SendCore(key, args, sourceFile, sourceLine, true);
#else
            SendCore(key, args, null, 0, true);
#endif
        }

        private void SendCore<T>(string key, T args, string sourceFile, int sourceLine, bool hasPayload)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnSend?.Invoke("String", key, hasPayload ? args : null, sourceFile, sourceLine);
#endif
            GetEvents(key, out var stringEvent);
            if (hasPayload)
                stringEvent.GetEvent<EasyEvent<T>>()?.Trigger(args);
            else
                stringEvent.GetEvent<EasyEvent>()?.Trigger();
        }

        /// <summary>发送可变参数字符串事件。</summary>
        [Obsolete("StringEvent params object[] payload allocates and is kept for compatibility. Use Send<T>(string, T) with a typed payload instead.")]
        public void Send(string key, params object[] args) => SendCore(key, args, null, 0, true);

        /// <summary>注册无参数字符串事件监听器。</summary>
        public LinkUnRegister Register(string key, Action onEvent)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnRegister?.Invoke("String", key, onEvent);
#endif
            GetEvents(key, out var stringEvent);
            return stringEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
        }

        /// <summary>注册带类型参数的字符串事件监听器。</summary>
        public LinkUnRegister<T> Register<T>(string key, Action<T> onEvent)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnRegister?.Invoke("String", key, onEvent);
#endif
            GetEvents(key, out var stringEvent);
            return stringEvent.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        }

        /// <summary>注册可变参数字符串事件监听器。</summary>
        public LinkUnRegister<object[]> Register(string key, Action<object[]> onEvent) => Register<object[]>(key, onEvent);

        /// <summary>清空绑定到指定字符串键的全部监听器。</summary>
        public void UnRegister(string key)
        {
            GetEvents(key, out var stringEvent);
            stringEvent.Clear();
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnClear?.Invoke("String", key);
#endif
        }

        /// <summary>注销一个无参数字符串事件监听器。</summary>
        public void UnRegister(string key, Action onEvent)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnUnRegister?.Invoke("String", key, onEvent);
#endif
            GetEvents(key, out var stringEvent);
            stringEvent.GetEvent<EasyEvent>()?.UnRegister(onEvent);
        }

        /// <summary>注销一个带类型参数的字符串事件监听器。</summary>
        public void UnRegister<T>(string key, Action<T> onEvent)
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnUnRegister?.Invoke("String", key, onEvent);
#endif
            GetEvents(key, out var stringEvent);
            stringEvent.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);
        }

        /// <summary>注销一个可变参数字符串事件监听器。</summary>
        public void UnRegister(string key, Action<object[]> onEvent) => UnRegister<object[]>(key, onEvent);

        /// <summary>清空全部字符串事件容器。</summary>
        public void Clear()
        {
            foreach (var kvp in mEventDic)
            {
                kvp.Value.Clear();
            }
            mEventDic.Clear();
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnClear?.Invoke("String", "*");
#endif
        }

        /// <summary>返回全部字符串事件，用于编辑器检查。</summary>
        public IReadOnlyDictionary<string, EasyEvents> GetAllEvents() => mEventDic;
    }
}
