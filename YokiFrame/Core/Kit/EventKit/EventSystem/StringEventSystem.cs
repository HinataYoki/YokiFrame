using System.Collections.Generic;
using System;

namespace YokiFrame
{
    public class StringEventSystem
    {
        private readonly Dictionary<string, EasyEvents> mEvents = new();

        public void Send(string key, params object[] args) => Send<object[]>(key, args);
        public void Send<T>(string key, T args)
        {
            if (mEvents.TryGetValue(key, out var typeEvent))
            {
                typeEvent.GetEvent<EasyEvent<T>>()?.Trigger(args);
            }
        }

        public CustomUnRegister Register(string key, Action<object[]> onEvent) => Register<object[]>(key, onEvent);
        public CustomUnRegister Register<T>(string key, Action<T> onEvent)
        {
            if (!mEvents.TryGetValue(key, out var stringEvent))
            {
                stringEvent = new();
                mEvents.Add(key, stringEvent);
            }
            return stringEvent.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        }

        public void UnRegister(string key, Action<object[]> onEvent) => UnRegister<object[]>(key, onEvent);
        public void UnRegister<T>(string key, Action<T> onEvent)
        {
            if (mEvents.TryGetValue(key, out var stringEvent))
            {
                stringEvent.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);
            }
        }

        public void Clear() => mEvents.Clear();
    }
}