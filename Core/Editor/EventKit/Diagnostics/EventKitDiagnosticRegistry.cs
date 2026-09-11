#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 消费 Runtime 最小观察通知，并维护 EventKit Workbench 所需的有界活动历史。
    /// </summary>
    internal static class EventKitDiagnosticRegistry
    {
        private const int MAX_ACTIVITY_COUNT = 200;
        private const int MAX_TYPE_NAME_CACHE = 256;
        private const int MAX_ENUM_KEY_CACHE = 256;

        private static readonly object sGate = new();
        private static readonly EventKitBoundedBuffer<EventKitActivityRecord> sActivities =
            new(MAX_ACTIVITY_COUNT);
        private static readonly Dictionary<Type, string> sTypeNames = new();
        private static readonly Dictionary<EnumEventKey, string> sEnumKeys = new();

        private static long sVersion;
        private static long sSequence;
        private static bool sTrackingEnabled;
        private static bool sInitialized;

        /// <summary>获取 EventKit Runtime 事实的当前单调版本。</summary>
        internal static long StateVersion
        {
            get
            {
                lock (sGate)
                {
                    return sVersion;
                }
            }
        }

        /// <summary>获取当前是否正在记录 EventKit Runtime 活动。</summary>
        internal static bool IsTrackingEnabled
        {
            get
            {
                lock (sGate)
                {
                    return sTrackingEnabled;
                }
            }
        }

        /// <summary>
        /// 安装 Runtime EventKit 最小 hook，并按默认值开启活动记录。
        /// 重复调用保持幂等，不会重置已由 <see cref="Configure"/> 设置的开关。
        /// </summary>
        internal static void EnsureInitialized()
        {
            lock (sGate)
            {
                if (sInitialized)
                {
                    return;
                }

                sInitialized = true;
                sTrackingEnabled = true;
            }

            EasyEventEditorHook.Activity -= OnActivity;
            EasyEventEditorHook.Activity += OnActivity;
            EasyEventEditorHook.SetTrackingEnabled(true);
        }

        /// <summary>
        /// 设置当前会话是否记录 EventKit Runtime 活动。
        /// </summary>
        /// <remarks>
        /// 关闭时同时卸载 Runtime hook 并清空已积累的活动历史与类型缓存，
        /// 使关闭后不再产生每次派发的 <c>GetInvocationList</c> 分配、锁与时间戳开销，
        /// 也不会让陈旧活动继续占用有界缓冲。
        /// </remarks>
        /// <param name="trackingEnabled">需要记录活动时为 true。</param>
        internal static void Configure(bool trackingEnabled)
        {
            lock (sGate)
            {
                if (sTrackingEnabled == trackingEnabled)
                {
                    return;
                }

                sTrackingEnabled = trackingEnabled;
                sVersion++;
                if (!trackingEnabled)
                {
                    sActivities.Clear();
                    sTypeNames.Clear();
                    sEnumKeys.Clear();
                }
            }

            // 关闭时卸载 hook，开启时重新订阅，保证开关双向都真正生效。
            EasyEventEditorHook.SetTrackingEnabled(trackingEnabled);
            EasyEventEditorHook.Activity -= OnActivity;
            if (trackingEnabled)
            {
                EasyEventEditorHook.Activity += OnActivity;
            }
        }

        /// <summary>创建不持锁的 EventKit 诊断快照。</summary>
        internal static EventKitDiagnosticSnapshot CreateSnapshot()
        {
            lock (sGate)
            {
                return new EventKitDiagnosticSnapshot(sVersion, sSequence, sActivities.ToArray());
            }
        }

        /// <summary>清空诊断历史和缓存，仅供隔离测试使用，生产生命周期不得调用。</summary>
        internal static void ResetForTests()
        {
            lock (sGate)
            {
                sActivities.Clear();
                sTypeNames.Clear();
                sEnumKeys.Clear();
                sVersion = 0L;
                sSequence = 0L;
                sTrackingEnabled = false;
                sInitialized = false;
            }
        }

        /// <summary>
        /// 把 Runtime 观察通知转换为无对象引用的 Workbench 活动记录。
        /// </summary>
        /// <param name="notification">由 EventKit Runtime 总线发布的最小通知。</param>
        private static void OnActivity(EventKitEditorNotification notification)
        {
            lock (sGate)
            {
                sSequence++;
                sVersion++;
                sActivities.Add(new EventKitActivityRecord(
                    sSequence,
                    GetKindName(notification.Kind),
                    GetChannelName(notification.Channel),
                    ResolveEventKey(notification),
                    ResolveTypeName(notification.PayloadType),
                    ResolveHandlerName(notification.Handler),
                    DateTime.Now.Ticks));
            }
        }

        /// <summary>把通知种类转换为稳定的协议文本。</summary>
        /// <param name="kind">Runtime 操作种类。</param>
        /// <returns>供 Workbench 展示的活动种类。</returns>
        private static string GetKindName(EventKitEditorNotificationKind kind)
        {
            switch (kind)
            {
                case EventKitEditorNotificationKind.Register:
                    return "register";
                case EventKitEditorNotificationKind.Unregister:
                    return "unregister";
                case EventKitEditorNotificationKind.Clear:
                    return "clear";
                default:
                    return "send";
            }
        }

        /// <summary>把通知通道转换为稳定的协议文本。</summary>
        /// <param name="channel">Runtime 事件通道。</param>
        /// <returns>供 Workbench 展示的通道名。</returns>
        private static string GetChannelName(EventKitEditorChannel channel)
        {
            switch (channel)
            {
                case EventKitEditorChannel.Enum:
                    return "Enum";
                case EventKitEditorChannel.String:
                    return "String";
                default:
                    return "Type";
            }
        }

        /// <summary>按通知通道解析稳定事件键；全通道清空使用星号键。</summary>
        /// <param name="notification">需要解析的 Runtime 通知。</param>
        /// <returns>可用于合并当前注册与活动历史的事件键。</returns>
        private static string ResolveEventKey(EventKitEditorNotification notification)
        {
            if (notification.Kind == EventKitEditorNotificationKind.Clear
                && notification.Channel != EventKitEditorChannel.String
                && notification.EnumKey.EnumType == null
                && notification.TypeKey == null)
            {
                return "*";
            }

            switch (notification.Channel)
            {
                case EventKitEditorChannel.Enum:
                    return ResolveEnumKey(notification.EnumKey);
                case EventKitEditorChannel.String:
                    return notification.StringKey ?? string.Empty;
                default:
                    return ResolveTypeName(notification.TypeKey);
            }
        }

        /// <summary>缓存并格式化完整类型身份，避免高频观察重复构造泛型类型文本。</summary>
        /// <param name="type">需要格式化的类型。</param>
        /// <returns>稳定的完整类型名。</returns>
        private static string ResolveTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (sTypeNames.TryGetValue(type, out string name))
            {
                return name;
            }

            name = EventKitTypeIdentity.Format(type);
            if (sTypeNames.Count < MAX_TYPE_NAME_CACHE)
            {
                sTypeNames.Add(type, name);
            }

            return name;
        }

        /// <summary>缓存并格式化完整枚举类型和值，避免高频观察重复反射枚举名称。</summary>
        /// <param name="key">枚举事件的运行时键。</param>
        /// <returns>稳定枚举事件键。</returns>
        private static string ResolveEnumKey(EnumEventKey key)
        {
            if (key.EnumType == null)
            {
                return EventKitTypeIdentity.FormatEnumEventKey(key);
            }

            if (sEnumKeys.TryGetValue(key, out string name))
            {
                return name;
            }

            name = FormatEnumKey(key);
            if (sEnumKeys.Count < MAX_ENUM_KEY_CACHE)
            {
                sEnumKeys.Add(key, name);
            }

            return name;
        }

        /// <summary>格式化缓存未命中的枚举事件键，并在无定义枚举值时保留底层数值。</summary>
        /// <param name="key">枚举事件的运行时键。</param>
        /// <returns>稳定枚举事件键。</returns>
        private static string FormatEnumKey(EnumEventKey key)
        {
            return EventKitTypeIdentity.FormatEnumEventKey(key);
        }

        /// <summary>把监听委托来源转换为稳定展示文本，避免诊断历史持有目标对象。</summary>
        /// <param name="handler">实际注册或注销的监听委托。</param>
        /// <returns>委托声明类型与方法名；没有委托时为空。</returns>
        private static string ResolveHandlerName(Delegate handler)
        {
            if (handler == null)
            {
                return string.Empty;
            }

            Type ownerType = handler.Method.DeclaringType;
            string ownerName = ownerType == null ? "Unknown" : ownerType.FullName;
            return ownerName + "." + handler.Method.Name;
        }
    }
}
#endif
