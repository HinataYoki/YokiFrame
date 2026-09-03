using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// EnumEvent 使用的稳定缓存键，用枚举类型和底层数值共同区分事件。
    /// </summary>
    public readonly struct EnumEventKey : IEquatable<EnumEventKey>
    {
        /// <summary>
        /// 枚举类型。
        /// </summary>
        public readonly Type EnumType;

        /// <summary>
        /// 枚举底层数值统一转换后的无符号值。
        /// </summary>
        public readonly ulong EnumValue;

        /// <summary>
        /// 创建枚举事件缓存键。
        /// </summary>
        /// <param name="enumType">枚举类型。</param>
        /// <param name="enumValue">枚举底层数值。</param>
        public EnumEventKey(Type enumType, ulong enumValue)
        {
            EnumType = enumType;
            EnumValue = enumValue;
        }

        /// <summary>
        /// 判断两个枚举事件键是否代表同一个枚举值。
        /// </summary>
        /// <param name="other">要比较的另一个键。</param>
        /// <returns>类型和值均一致时返回 true。</returns>
        public bool Equals(EnumEventKey other)
        {
            return EnumType == other.EnumType && EnumValue == other.EnumValue;
        }

        /// <summary>
        /// 判断指定对象是否为相同枚举事件键。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>对象为同值 EnumEventKey 时返回 true。</returns>
        public override bool Equals(object obj)
        {
            return obj is EnumEventKey other && Equals(other);
        }

        /// <summary>
        /// 获取组合后的哈希值，供字典查找使用。
        /// </summary>
        /// <returns>当前键的哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int typeHash = EnumType != null ? EnumType.GetHashCode() : 0;
                return (typeHash * 397) ^ EnumValue.GetHashCode();
            }
        }

        /// <summary>
        /// 判断两个枚举事件缓存键是否相等。
        /// </summary>
        /// <param name="left">左侧操作数。</param>
        /// <param name="right">右侧操作数。</param>
        /// <returns>两个键相等时返回 true，否则返回 false。</returns>
        public static bool operator ==(EnumEventKey left, EnumEventKey right) => left.Equals(right);

        /// <summary>
        /// 判断两个枚举事件缓存键是否不相等。
        /// </summary>
        /// <param name="left">左侧操作数。</param>
        /// <param name="right">右侧操作数。</param>
        /// <returns>两个键不相等时返回 true，否则返回 false。</returns>
        public static bool operator !=(EnumEventKey left, EnumEventKey right) => !left.Equals(right);
    }

    /// <summary>以枚举值为键的事件总线。</summary>
    public sealed class EnumEvent
    {
        private readonly Dictionary<EnumEventKey, EasyEvents> mEventDic = new Dictionary<EnumEventKey, EasyEvents>();

        /// <summary>
        /// 发送无参数枚举事件。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        public void Send<TEnum>(TEnum key) where TEnum : Enum
        {
            SendCore<TEnum, object>(key, null, false);
        }

        /// <summary>
        /// 发送带类型负载的枚举事件。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <typeparam name="TArgs">事件负载类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="args">事件负载。</param>
        public void Send<TEnum, TArgs>(TEnum key, TArgs args) where TEnum : Enum
        {
            SendCore(key, args, true);
        }

        /// <summary>
        /// 发送可变参数枚举事件；仅为兼容旧代码保留。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="args">可变参数负载。</param>
        [Obsolete("params object[] 会产生分配且缺少类型安全，请使用 Send<TEnum, TArgs>。")]
        public void Send<TEnum>(TEnum key, params object[] args) where TEnum : Enum
        {
            SendCore<TEnum, object[]>(key, args, true);
        }

        /// <summary>
        /// 注册无参数枚举事件监听器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister Register<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            EnumEventKey cacheKey = BuildCacheKey(key);
            EasyEvents enumEvent = GetOrCreateEvents(cacheKey);
            LinkUnRegister token = enumEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
#if UNITY_EDITOR || (GODOT && TOOLS)
            var registerNotification = new EventKitEditorNotification(
                EventKitEditorNotificationKind.Register,
                cacheKey,
                null,
                onEvent);
            if (EasyEventEditorHook.Publish(registerNotification))
            {
                return token.WithEditorUnregisterNotification(new EventKitEditorNotification(
                    EventKitEditorNotificationKind.Unregister,
                    cacheKey,
                    null,
                    onEvent));
            }
#endif
            return token;
        }

        /// <summary>
        /// 注册带类型负载的枚举事件监听器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <typeparam name="TArgs">事件负载类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister<TArgs> Register<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            EnumEventKey cacheKey = BuildCacheKey(key);
            EasyEvents enumEvent = GetOrCreateEvents(cacheKey);
            LinkUnRegister<TArgs> token = enumEvent.GetOrAddEvent<EasyEvent<TArgs>>().Register(onEvent);
#if UNITY_EDITOR || (GODOT && TOOLS)
            var registerNotification = new EventKitEditorNotification(
                EventKitEditorNotificationKind.Register,
                cacheKey,
                typeof(TArgs),
                onEvent);
            if (EasyEventEditorHook.Publish(registerNotification))
            {
                return token.WithEditorUnregisterNotification(new EventKitEditorNotification(
                    EventKitEditorNotificationKind.Unregister,
                    cacheKey,
                    typeof(TArgs),
                    onEvent));
            }
#endif
            return token;
        }

        /// <summary>
        /// 注册可变参数枚举事件监听器；仅为兼容旧代码保留。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        [Obsolete("params object[] 会产生分配且缺少类型安全，请使用 Register<TEnum, TArgs> / UnRegister<TEnum, TArgs>。")]
        public LinkUnRegister<object[]> Register<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum
        {
            return Register<TEnum, object[]>(key, onEvent);
        }

        /// <summary>
        /// 清空绑定到指定枚举键的全部监听器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        public void UnRegister<TEnum>(TEnum key) where TEnum : Enum
        {
            EasyEvents enumEvent;
            EnumEventKey cacheKey;
            if (!TryGetEvents(key, out cacheKey, out enumEvent))
            {
                return;
            }

            enumEvent.Clear();
            mEventDic.Remove(cacheKey);

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Clear,
                cacheKey,
                null,
                null));
#endif
        }

        /// <summary>
        /// 注销一个无参数枚举事件监听器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="onEvent">需要移除的监听器。</param>
        public void UnRegister<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
            EasyEvents enumEvent;
            EnumEventKey cacheKey;
            if (!TryGetEvents(key, out cacheKey, out enumEvent))
            {
                return;
            }

            EasyEvent easyEvent = enumEvent.GetEvent<EasyEvent>();
            if (easyEvent == null || !easyEvent.UnRegister(onEvent))
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Unregister,
                cacheKey,
                null,
                onEvent));
#endif
        }

        /// <summary>
        /// 注销一个带类型负载的枚举事件监听器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <typeparam name="TArgs">事件负载类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="onEvent">需要移除的监听器。</param>
        public void UnRegister<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
            EasyEvents enumEvent;
            EnumEventKey cacheKey;
            if (!TryGetEvents(key, out cacheKey, out enumEvent))
            {
                return;
            }

            EasyEvent<TArgs> easyEvent = enumEvent.GetEvent<EasyEvent<TArgs>>();
            if (easyEvent == null || !easyEvent.UnRegister(onEvent))
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Unregister,
                cacheKey,
                typeof(TArgs),
                onEvent));
#endif
        }

        /// <summary>
        /// 注销一个可变参数枚举事件监听器；仅为兼容旧代码保留。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="onEvent">需要移除的监听器。</param>
        [Obsolete("params object[] 会产生分配且缺少类型安全，请使用 Register<TEnum, TArgs> / UnRegister<TEnum, TArgs>。")]
        public void UnRegister<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum
        {
            UnRegister<TEnum, object[]>(key, onEvent);
        }

        /// <summary>
        /// 清空全部枚举事件监听器。
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<EnumEventKey, EasyEvents> pair in mEventDic)
            {
                pair.Value.Clear();
            }

            mEventDic.Clear();
#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Clear,
                default(EnumEventKey),
                null,
                null));
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 返回全部枚举事件容器，用于编辑器检查或诊断。
        /// </summary>
        /// <returns>枚举事件容器字典。</returns>
        public IReadOnlyDictionary<EnumEventKey, EasyEvents> GetAllEvents()
        {
            return mEventDic;
        }
#endif

        /// <summary>
        /// 执行枚举事件发送，并根据是否存在负载选择对应 EasyEvent 容器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <typeparam name="TArgs">事件负载类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="args">事件负载。</param>
        /// <param name="hasPayload">是否发送负载事件。</param>
        private void SendCore<TEnum, TArgs>(
            TEnum key,
            TArgs args,
            bool hasPayload) where TEnum : Enum
        {
            EnumEventKey cacheKey = BuildCacheKey(key);
#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Send,
                cacheKey,
                hasPayload ? typeof(TArgs) : null,
                null));
#endif
            EasyEvents enumEvent;
            if (!mEventDic.TryGetValue(cacheKey, out enumEvent))
            {
                return;
            }

            if (hasPayload)
            {
                enumEvent.GetEvent<EasyEvent<TArgs>>()?.Trigger(args);
                return;
            }

            enumEvent.GetEvent<EasyEvent>()?.Trigger();
        }

        /// <summary>
        /// 获取指定缓存键的事件容器；不存在时创建。
        /// </summary>
        /// <param name="cacheKey">已经构造的枚举缓存键。</param>
        /// <returns>事件容器集合。</returns>
        private EasyEvents GetOrCreateEvents(EnumEventKey cacheKey)
        {
            EasyEvents enumEvent;
            if (!mEventDic.TryGetValue(cacheKey, out enumEvent))
            {
                enumEvent = new EasyEvents();
                mEventDic.Add(cacheKey, enumEvent);
            }

            return enumEvent;
        }

        /// <summary>
        /// 尝试获取指定枚举键的事件容器；不存在时不创建空容器。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <param name="cacheKey">输出已经构造的枚举缓存键。</param>
        /// <param name="enumEvent">输出事件容器集合。</param>
        /// <returns>存在对应容器时返回 true。</returns>
        private bool TryGetEvents<TEnum>(TEnum key, out EnumEventKey cacheKey, out EasyEvents enumEvent) where TEnum : Enum
        {
            cacheKey = BuildCacheKey(key);
            return mEventDic.TryGetValue(cacheKey, out enumEvent);
        }

        /// <summary>
        /// 构造用于字典缓存的枚举键。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <returns>缓存键。</returns>
        private static EnumEventKey BuildCacheKey<TEnum>(TEnum key) where TEnum : Enum
        {
            return new EnumEventKey(typeof(TEnum), EnumValueCache<TEnum>.ToUInt64(key));
        }

        /// <summary>
        /// 把未命中的枚举值转换为统一的 ulong 缓存值；正常热路径由泛型缓存直接返回。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="key">枚举事件键。</param>
        /// <returns>转换后的无符号数值。</returns>
        private static ulong ToEnumKeyValue<TEnum>(TEnum key) where TEnum : Enum
        {
            IConvertible value = key;
            return ConvertEnumValue(value, EnumValueCache<TEnum>.UnderlyingTypeCode);
        }

        /// <summary>
        /// 根据枚举底层类型执行无装箱分支之外的统一数值转换。
        /// </summary>
        /// <param name="value">枚举值的 IConvertible 视图。</param>
        /// <param name="typeCode">枚举底层类型码。</param>
        /// <returns>转换后的无符号数值。</returns>
        private static ulong ConvertEnumValue(IConvertible value, TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.SByte:
                    return unchecked((ulong)value.ToSByte(null));
                case TypeCode.Byte:
                    return value.ToByte(null);
                case TypeCode.Int16:
                    return unchecked((ulong)value.ToInt16(null));
                case TypeCode.UInt16:
                    return value.ToUInt16(null);
                case TypeCode.Int32:
                    return unchecked((ulong)value.ToInt32(null));
                case TypeCode.UInt32:
                    return value.ToUInt32(null);
                case TypeCode.Int64:
                    return unchecked((ulong)value.ToInt64(null));
                case TypeCode.UInt64:
                    return value.ToUInt64(null);
                default:
                    throw new InvalidOperationException("Unsupported enum underlying type.");
            }
        }

        /// <summary>
        /// 为每个枚举类型缓存有限数量的已见底层值，避免高频 Send 在接口转换处重复装箱。
        /// </summary>
        /// <typeparam name="TEnum">当前泛型枚举类型。</typeparam>
        private static class EnumValueCache<TEnum> where TEnum : Enum
        {
            private const int MAX_CACHED_VALUES = 128;
            private static readonly object sLock = new();
            private static volatile Dictionary<TEnum, ulong> sValues = new();

            /// <summary>获取当前枚举类型的底层类型码，避免每次查找反射元数据。</summary>
            internal static readonly TypeCode UnderlyingTypeCode;

            /// <summary>
            /// 校验泛型实参为具体枚举类型后再缓存底层类型码；每个封闭泛型类型仅执行一次。
            /// </summary>
            static EnumValueCache()
            {
                if (!typeof(TEnum).IsEnum)
                {
                    throw new NotSupportedException("EnumEvent 需要具体枚举类型作为泛型实参，不支持 System.Enum。");
                }

                UnderlyingTypeCode = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));
            }

            /// <summary>读取已缓存转换结果；命中时零锁极速返回，冷路径安全加锁并以写时复制发布快照。</summary>
            /// <param name="value">需要转换的枚举值。</param>
            /// <returns>用于 EventKit 字典的无符号底层值。</returns>
            internal static ulong ToUInt64(TEnum value)
            {
                if (sValues.TryGetValue(value, out ulong cachedValue))
                {
                    return cachedValue;
                }

                lock (sLock)
                {
                    if (sValues.TryGetValue(value, out cachedValue))
                    {
                        return cachedValue;
                    }

                    ulong convertedValue = ToEnumKeyValue(value);
                    if (sValues.Count < MAX_CACHED_VALUES)
                    {
                        Dictionary<TEnum, ulong> copy = new(sValues);
                        copy[value] = convertedValue;
                        sValues = copy;
                    }

                    return convertedValue;
                }
            }
        }
    }
}
