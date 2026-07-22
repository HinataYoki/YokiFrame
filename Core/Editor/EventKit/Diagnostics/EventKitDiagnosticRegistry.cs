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

        /// <summary>
        /// 安装 Runtime EventKit 最小 hook；首次真正需要 Workbench 观察时才开始记录活动。
        /// </summary>
        internal static void EnsureInitialized()
        {
            EasyEventEditorHook.Activity -= OnActivity;
            EasyEventEditorHook.Activity += OnActivity;
            EasyEventEditorHook.SetTrackingEnabled(true);
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
