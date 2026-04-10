using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Small typed event container that stores multiple <see cref="IEasyEvent"/> instances by event class.
    /// </summary>
    public class EasyEvents
    {
        private readonly Dictionary<Type, IEasyEvent> mTypeEventDic = new();

        /// <summary>
        /// Adds an empty event container of the specified type.
        /// </summary>
        public void AddEvent<T>() where T : IEasyEvent, new() => mTypeEventDic.Add(typeof(T), new T());

        /// <summary>
        /// Gets a previously added event container.
        /// </summary>
        public T GetEvent<T>() where T : IEasyEvent
        {
            return mTypeEventDic.TryGetValue(typeof(T), out var typeEvent) ? (T)typeEvent : default;
        }

        /// <summary>
        /// Gets an event container or creates it on first access.
        /// </summary>
        public T GetOrAddEvent<T>() where T : IEasyEvent, new()
        {
            var type = typeof(T);
            if (!mTypeEventDic.TryGetValue(type, out var typeEvent))
            {
                typeEvent = new T();
                mTypeEventDic.Add(type, typeEvent);
            }

            return (T)typeEvent;
        }

        /// <summary>
        /// Removes all cached event containers.
        /// </summary>
        public void Clear() => mTypeEventDic.Clear();

        /// <summary>
        /// Returns all registered event containers for diagnostics or editor inspection.
        /// </summary>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents() => mTypeEventDic;
    }
}
