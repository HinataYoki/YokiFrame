using System.Collections.Generic;
using System;

namespace YokiFrame
{
    public class EnumEventSystem
    {
        private readonly Dictionary<(Type, int), EasyEvents> mEvents = new();

        public void Send<T>(T key, params object[] args) where T : Enum, IConvertible => Send<T, object[]>(key, args);
        public void Send<TEnum, TArgs>(TEnum key, TArgs args) where TEnum : Enum, IConvertible
        {
            var type = typeof(TEnum);
            var intKey = Convert.ToInt32(key);

            if (mEvents.TryGetValue((type, intKey), out var typeEvent))
            {
                typeEvent.GetEvent<EasyEvent<TArgs>>()?.Trigger(args);
            }
        }

        public CustomUnRegister Register<T>(T key, Action<object[]> onEvent) where T : Enum, IConvertible => Register<T, object[]>(key, onEvent);
        public CustomUnRegister Register<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum, IConvertible
        {
            var type = typeof(TEnum);
            var intKey = Convert.ToInt32(key);

            if (!mEvents.TryGetValue((type, intKey), out var typeEvent))
            {
                typeEvent = new();
                mEvents.Add((type, intKey), typeEvent);
            }

            return typeEvent.GetOrAddEvent<EasyEvent<TArgs>>().Register(onEvent);
        }

        public void UnRegister<T>(T key, Action<object[]> onEvent) where T : Enum, IConvertible => UnRegister<T, object[]>(key, onEvent);
        public void UnRegister<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum, IConvertible
        {
            var type = typeof(TEnum);
            var intKey = Convert.ToInt32(key);

            if (mEvents.TryGetValue((type, intKey), out var typeEvent))
            {
                typeEvent.GetEvent<EasyEvent<TArgs>>()?.UnRegister(onEvent);
            }
        }

        public void Clear() => mEvents.Clear();
    }
}