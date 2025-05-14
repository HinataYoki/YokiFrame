using System;

namespace YokiFrame
{
    public class TypeEventSystem
    {
        private readonly EasyEvents mEvents = new();

        public void Send<T>(T args = default) => mEvents.GetEvent<EasyEvent<T>>()?.Trigger(args);

        public CustomUnRegister Register<T>(Action<T> onEvent) => mEvents.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);

        public void UnRegister<T>(Action<T> onEvent) => mEvents.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);

        public void Clear() => mEvents.Clear();
    }
}