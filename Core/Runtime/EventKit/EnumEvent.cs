using System;
using System.Collections.Generic;
#if UNITY_EDITOR || GODOT
using System.Runtime.CompilerServices;
#endif

namespace YokiFrame
{
    /// <summary>
    /// EnumEvent 使用的缓存键，用于避免 tuple 分配和额外哈希开销。
    /// </summary>
    public readonly struct EnumEventKey : IEquatable<EnumEventKey>
    {
        public readonly Type EnumType;
        public readonly ulong EnumValue;

        public EnumEventKey(Type enumType, ulong enumValue)
        {
            EnumType = enumType;
            EnumValue = enumValue;
        }

        public bool Equals(EnumEventKey other) => EnumType == other.EnumType && EnumValue == other.EnumValue;
        public override bool Equals(object obj) => obj is EnumEventKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EnumType, EnumValue);
    }

    /// <summary>
    /// 以枚举值为键的运行时事件中心。
    /// </summary>
    public class EnumEvent
    {
        private readonly Dictionary<EnumEventKey, EasyEvents> mEventDic = new();

        /// <summary>获取指定枚举键的事件容器；不存在时自动创建。</summary>
        private void GetEvents<TEnum>(TEnum key, out EasyEvents enumEvent) where TEnum : Enum, IConvertible
        {
            var type = typeof(TEnum);
            var cacheKey = new EnumEventKey(type, ToEnumKeyValue(key));
            if (!mEventDic.TryGetValue(cacheKey, out enumEvent))
            {
                enumEvent = new EasyEvents();
                mEventDic.Add(cacheKey, enumEvent);
            }
        }

        private static ulong ToEnumKeyValue<TEnum>(TEnum key) where TEnum : Enum, IConvertible
        {
            return Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum))) switch
            {
                TypeCode.SByte => unchecked((ulong)key.ToSByte(null)),
                TypeCode.Byte => key.ToByte(null),
                TypeCode.Int16 => unchecked((ulong)key.ToInt16(null)),
                TypeCode.UInt16 => key.ToUInt16(null),
                TypeCode.Int32 => unchecked((ulong)key.ToInt32(null)),
                TypeCode.UInt32 => key.ToUInt32(null),
                TypeCode.Int64 => unchecked((ulong)key.ToInt64(null)),
                TypeCode.UInt64 => key.ToUInt64(null),
                _ => throw new InvalidOperationException("Unsupported enum underlying type."),
            };
        }

        /// <summary>发送无参数枚举事件。</summary>
        public void Send<TEnum>(TEnum key) where TEnum : Enum
        {
            SendCore<TEnum, object>(key, null, null, 0, false);
        }

        /// <summary>发送带类型参数的枚举事件。</summary>
        public void Send<TEnum, TArgs>(TEnum key, TArgs args
#if UNITY_EDITOR || GODOT
            , [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = 0
#endif
        ) where TEnum : Enum
        {
#if UNITY_EDITOR || GODOT
            SendCore(key, args, sourceFile, sourceLine, true);
#else
            SendCore(key, args, null, 0, true);
#endif
        }

        private void SendCore<TEnum, TArgs>(TEnum key, TArgs args, string sourceFile, int sourceLine, bool hasPayload) where TEnum : Enum
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnSend?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", hasPayload ? args : null, sourceFile, sourceLine);
#endif
            GetEvents(key, out var enumEvent);
            if (hasPayload)
                enumEvent.GetEvent<EasyEvent<TArgs>>()?.Trigger(args);
            else
                enumEvent.GetEvent<EasyEvent>()?.Trigger();
        }

        /// <summary>发送带可变参数的枚举事件。</summary>
        [Obsolete("EnumEvent params object[] payload allocates and is kept for compatibility. Use Send<TEnum, TArgs> with a typed payload instead.")]
        public void Send<TEnum>(TEnum key, params object[] args) where TEnum : Enum
        {
            SendCore<TEnum, object[]>(key, args, null, 0, true);
        }

        /// <summary>注册无参数枚举事件监听器。</summary>
        public LinkUnRegister Register<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnRegister?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", onEvent);
#endif
            GetEvents(key, out var enumEvent);
            return enumEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
        }

        /// <summary>注册带类型参数的枚举事件监听器。</summary>
        public LinkUnRegister<TArgs> Register<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnRegister?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", onEvent);
#endif
            GetEvents(key, out var enumEvent);
            return enumEvent.GetOrAddEvent<EasyEvent<TArgs>>().Register(onEvent);
        }

        /// <summary>注册可变参数枚举事件监听器。</summary>
        public LinkUnRegister<object[]> Register<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum
            => Register<TEnum, object[]>(key, onEvent);

        /// <summary>清空绑定到指定枚举键的全部监听器。</summary>
        public void UnRegister<TEnum>(TEnum key) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.Clear();
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnClear?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}");
#endif
        }

        /// <summary>注销一个无参数枚举事件监听器。</summary>
        public void UnRegister<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnUnRegister?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", onEvent);
#endif
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent>()?.UnRegister(onEvent);
        }

        /// <summary>注销一个带类型参数的枚举事件监听器。</summary>
        public void UnRegister<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnUnRegister?.Invoke("Enum", $"{typeof(TEnum).Name}.{key}", onEvent);
#endif
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent<TArgs>>()?.UnRegister(onEvent);
        }

        /// <summary>注销一个可变参数枚举事件监听器。</summary>
        public void UnRegister<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum
            => UnRegister<TEnum, object[]>(key, onEvent);

        /// <summary>清空全部枚举事件容器。</summary>
        public void Clear()
        {
            mEventDic.Clear();
#if UNITY_EDITOR || GODOT
            EasyEventEditorHook.OnClear?.Invoke("Enum", "*");
#endif
        }

        /// <summary>返回全部枚举事件，用于编辑器检查。</summary>
        public IReadOnlyDictionary<EnumEventKey, EasyEvents> GetAllEvents() => mEventDic;
    }
}
