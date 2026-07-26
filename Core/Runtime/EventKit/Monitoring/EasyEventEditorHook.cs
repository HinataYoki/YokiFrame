#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Diagnostics;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// EventKit Runtime 操作种类；仅作为 Runtime 到 Editor 的最小观察契约。
    /// </summary>
    internal enum EventKitEditorNotificationKind
    {
        Register,
        Unregister,
        Send,
        Clear
    }

    /// <summary>
    /// EventKit Runtime 事件通道；避免 Runtime 直接依赖 Editor 的展示字符串。
    /// </summary>
    internal enum EventKitEditorChannel
    {
        Type,
        Enum,
        String
    }

    /// <summary>
    /// EventKit Runtime 向 Editor 发布的最小观察记录；不包含调用点、堆栈或业务负载。
    /// </summary>
    internal readonly struct EventKitEditorNotification
    {
        /// <summary>创建 Type 通道通知。</summary>
        /// <param name="kind">当前 Runtime 操作种类。</param>
        /// <param name="typeKey">Type 通道的事件键。</param>
        /// <param name="payloadType">发送或注册的负载类型；无负载时为空。</param>
        /// <param name="handler">注册或注销的监听委托；发送和清理时为空。</param>
        internal EventKitEditorNotification(
            EventKitEditorNotificationKind kind,
            Type typeKey,
            Type payloadType,
            Delegate handler)
        {
            Kind = kind;
            Channel = EventKitEditorChannel.Type;
            TypeKey = typeKey;
            EnumKey = default;
            StringKey = null;
            PayloadType = payloadType;
            Handler = handler;
        }

        /// <summary>创建 Enum 通道通知。</summary>
        /// <param name="kind">当前 Runtime 操作种类。</param>
        /// <param name="enumKey">Enum 通道的事件键。</param>
        /// <param name="payloadType">发送或注册的负载类型；无负载时为空。</param>
        /// <param name="handler">注册或注销的监听委托；发送和清理时为空。</param>
        internal EventKitEditorNotification(
            EventKitEditorNotificationKind kind,
            EnumEventKey enumKey,
            Type payloadType,
            Delegate handler)
        {
            Kind = kind;
            Channel = EventKitEditorChannel.Enum;
            TypeKey = null;
            EnumKey = enumKey;
            StringKey = null;
            PayloadType = payloadType;
            Handler = handler;
        }

        /// <summary>创建 String 通道通知。</summary>
        /// <param name="kind">当前 Runtime 操作种类。</param>
        /// <param name="stringKey">String 通道的事件键。</param>
        /// <param name="payloadType">发送或注册的负载类型；无负载时为空。</param>
        /// <param name="handler">注册或注销的监听委托；发送和清理时为空。</param>
        internal EventKitEditorNotification(
            EventKitEditorNotificationKind kind,
            string stringKey,
            Type payloadType,
            Delegate handler)
        {
            Kind = kind;
            Channel = EventKitEditorChannel.String;
            TypeKey = null;
            EnumKey = default;
            StringKey = stringKey;
            PayloadType = payloadType;
            Handler = handler;
        }

        internal EventKitEditorNotificationKind Kind { get; }
        internal EventKitEditorChannel Channel { get; }
        internal Type TypeKey { get; }
        internal EnumEventKey EnumKey { get; }
        internal string StringKey { get; }
        internal Type PayloadType { get; }
        internal Delegate Handler { get; }
    }

    /// <summary>
    /// EventKit Runtime 发布诊断事件的轻量端口；Player 不包含该类型，Editor 回调异常不会中断业务总线。
    /// </summary>
    internal static class EasyEventEditorHook
    {
        private static int sTrackingEnabled;

        /// <summary>
        /// Editor/Tools 订阅的最小 Runtime 活动通知。
        /// </summary>
        internal static event Action<EventKitEditorNotification> Activity;

        /// <summary>
        /// 获取当前是否存在需要记录 Runtime 活动的 Editor/Tools 观察者。
        /// </summary>
        internal static bool IsTrackingEnabled => Volatile.Read(ref sTrackingEnabled) != 0;

        /// <summary>
        /// 由 Editor/Tools 注册表开启或关闭 Runtime 观察；Player 不包含该状态。
        /// </summary>
        /// <param name="enabled">需要记录活动时为 true。</param>
        internal static void SetTrackingEnabled(bool enabled)
        {
            Interlocked.Exchange(ref sTrackingEnabled, enabled ? 1 : 0);
        }

        /// <summary>
        /// 发布一次 Runtime 活动；没有观察者时只执行原子开关和空委托检查。
        /// </summary>
        /// <param name="notification">不持有业务负载的最小活动描述。</param>
        /// <returns>至少一个观察者收到通知时返回 true。</returns>
        internal static bool Publish(EventKitEditorNotification notification)
        {
            if (!IsTrackingEnabled)
            {
                return false;
            }

            Action<EventKitEditorNotification> callback = Activity;
            if (callback == null)
            {
                return false;
            }

            Delegate[] callbacks = callback.GetInvocationList();
            bool delivered = false;
            for (int index = 0; index < callbacks.Length; index++)
            {
                try
                {
                    ((Action<EventKitEditorNotification>)callbacks[index]).Invoke(notification);
                    delivered = true;
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }

            return delivered;
        }
    }
}
#endif
