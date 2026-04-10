using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Runtime event hub keyed by payload type.
    /// </summary>
    public class TypeEvent
    {
        private readonly EasyEvents mEventDic = new();

        /// <summary>
        /// Sends an event keyed by payload type.
        /// </summary>
        public void Send<T>(T args = default)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnSend?.Invoke("Type", typeof(T).Name, args);
#endif
            mEventDic.GetEvent<EasyEvent<T>>()?.Trigger(args);
        }

        /// <summary>
        /// Registers a typed listener.
        /// </summary>
        public LinkUnRegister<T> Register<T>(Action<T> onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnRegister?.Invoke(onEvent);
#endif
            return mEventDic.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        }

        /// <summary>
        /// Unregisters one typed listener.
        /// </summary>
        public void UnRegister<T>(Action<T> onEvent)
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnUnRegister?.Invoke(onEvent);
#endif
            mEventDic.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);
        }

        /// <summary>
        /// Clears all registered typed events.
        /// </summary>
        public void Clear() => mEventDic.Clear();

        /// <summary>
        /// Returns all registered typed events for editor inspection.
        /// </summary>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents() => mEventDic.GetAllEvents();
    }
}
