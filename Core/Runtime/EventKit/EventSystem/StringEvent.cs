using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Runtime event hub keyed by strings.
    /// </summary>
    /// <remarks>
    /// This API is kept for compatibility, but new systems should prefer <see cref="TypeEvent"/> or
    /// <see cref="EnumEvent"/> because they are easier to refactor and provide better type safety.
    /// </remarks>
    [Obsolete("StringEvent 存在类型安全隐患且重构困难，建议优先使用 TypeEvent 或 EnumEvent。")]
    public class StringEvent
    {
        private readonly Dictionary<string, EasyEvents> mEventDic = new();

        /// <summary>
        /// Gets or creates the event container for a specific string key.
        /// </summary>
        private void GetEvents(string key, out EasyEvents stringEvent)
        {
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                stringEvent = new EasyEvents();
                mEventDic.Add(key, stringEvent);
            }
        }

        /// <summary>
        /// Sends a parameterless string-keyed event.
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
        /// Sends a typed string-keyed event.
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
        /// Sends a variadic string-keyed event.
        /// </summary>
        public void Send(string key, params object[] args) => Send<object[]>(key, args);

        /// <summary>
        /// Registers a parameterless string-keyed listener.
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
        /// Registers a typed string-keyed listener.
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
        /// Registers a variadic string-keyed listener.
        /// </summary>
        public LinkUnRegister<object[]> Register(string key, Action<object[]> onEvent) => Register<object[]>(key, onEvent);

        /// <summary>
        /// Clears all listeners bound to one string key.
        /// </summary>
        public void UnRegister(string key)
        {
            GetEvents(key, out var stringEvent);
            stringEvent.Clear();
        }

        /// <summary>
        /// Unregisters one parameterless string-keyed listener.
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
        /// Unregisters one typed string-keyed listener.
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
        /// Unregisters one variadic string-keyed listener.
        /// </summary>
        public void UnRegister(string key, Action<object[]> onEvent) => UnRegister<object[]>(key, onEvent);

        /// <summary>
        /// Clears all string-keyed event containers.
        /// </summary>
        public void Clear() => mEventDic.Clear();

        /// <summary>
        /// Returns all string-keyed events for editor inspection.
        /// </summary>
        public IReadOnlyDictionary<string, EasyEvents> GetAllEvents() => mEventDic;
    }
}
