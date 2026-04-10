using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace YokiFrame
{
    /// <summary>
    /// Cached key used by <see cref="EnumEvent"/> to avoid tuple allocation and hashing overhead.
    /// </summary>
    public readonly struct EnumEventKey : IEquatable<EnumEventKey>
    {
        /// <summary>
        /// Enum type.
        /// </summary>
        public readonly Type EnumType;

        /// <summary>
        /// Enum value converted to <see cref="int"/>.
        /// </summary>
        public readonly int EnumValue;

        /// <summary>
        /// Creates a cached enum-event key.
        /// </summary>
        public EnumEventKey(Type enumType, int enumValue)
        {
            EnumType = enumType;
            EnumValue = enumValue;
        }

        /// <summary>
        /// Compares two enum-event keys.
        /// </summary>
        public bool Equals(EnumEventKey other) => EnumType == other.EnumType && EnumValue == other.EnumValue;

        /// <summary>
        /// Compares this key to another object.
        /// </summary>
        public override bool Equals(object obj) => obj is EnumEventKey other && Equals(other);

        /// <summary>
        /// Returns the combined hash code.
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(EnumType, EnumValue);
    }

    /// <summary>
    /// Runtime event hub keyed by enum values.
    /// </summary>
    public class EnumEvent
    {
        private readonly Dictionary<EnumEventKey, EasyEvents> mEventDic = new();

        /// <summary>
        /// Gets or creates the event container for a specific enum key.
        /// </summary>
        private void GetEvents<TEnum>(TEnum key, out EasyEvents enumEvent) where TEnum : Enum, IConvertible
        {
            var type = typeof(TEnum);
#if UNITY_2021_1_OR_NEWER
            var intKey = UnsafeUtility.As<TEnum, int>(ref key);
#else
            var intKey = key.ToInt32(null);
#endif
            var cacheKey = new EnumEventKey(type, intKey);
            if (!mEventDic.TryGetValue(cacheKey, out enumEvent))
            {
                enumEvent = new EasyEvents();
                mEventDic.Add(cacheKey, enumEvent);
            }
        }

        /// <summary>
        /// Sends a parameterless enum event.
        /// </summary>
        public void Send<TEnum>(TEnum key) where TEnum : Enum
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnSend?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", null);
#endif
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent>()?.Trigger();
        }

        /// <summary>
        /// Sends an enum event with a typed payload.
        /// </summary>
        public void Send<TEnum, TArgs>(TEnum key, TArgs args) where TEnum : Enum
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnSend?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", args);
#endif
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent<TArgs>>()?.Trigger(args);
        }

        /// <summary>
        /// Sends an enum event with a variadic payload.
        /// </summary>
        public void Send<TEnum>(TEnum key, params object[] args) where TEnum : Enum => Send<TEnum, object[]>(key, args);

        /// <summary>
        /// Registers a parameterless enum listener.
        /// </summary>
        public LinkUnRegister Register<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var enumEvent);
            return enumEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
        }

        /// <summary>
        /// Registers a typed enum listener.
        /// </summary>
        public LinkUnRegister<TArgs> Register<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var enumEvent);
            return enumEvent.GetOrAddEvent<EasyEvent<TArgs>>().Register(onEvent);
        }

        /// <summary>
        /// Registers a variadic enum listener.
        /// </summary>
        public LinkUnRegister<object[]> Register<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum => Register<TEnum, object[]>(key, onEvent);

        /// <summary>
        /// Clears all listeners bound to one enum key.
        /// </summary>
        public void UnRegister<TEnum>(TEnum key) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.Clear();
        }

        /// <summary>
        /// Unregisters one parameterless enum listener.
        /// </summary>
        public void UnRegister<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnUnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent>()?.UnRegister(onEvent);
        }

        /// <summary>
        /// Unregisters one typed enum listener.
        /// </summary>
        public void UnRegister<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR
            EasyEventEditorHook.OnUnRegister?.Invoke(onEvent);
#endif
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent<TArgs>>()?.UnRegister(onEvent);
        }

        /// <summary>
        /// Unregisters one variadic enum listener.
        /// </summary>
        public void UnRegister<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum => UnRegister<TEnum, object[]>(key, onEvent);

        /// <summary>
        /// Clears all enum-keyed event containers.
        /// </summary>
        public void Clear() => mEventDic.Clear();

        /// <summary>
        /// Returns all enum-keyed events for editor inspection.
        /// </summary>
        public IReadOnlyDictionary<EnumEventKey, EasyEvents> GetAllEvents() => mEventDic;
    }
}
