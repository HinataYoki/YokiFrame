#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 从 EventKit Runtime 总线读取当前监听器，并与有界活动历史合并为 Workbench 快照。
    /// </summary>
    internal static class EventKitSnapshotBuilder
    {
        private const string TYPE_CHANNEL = "Type";
        private const string ENUM_CHANNEL = "Enum";
        private const string STRING_CHANNEL = "String";

        /// <summary>创建当前 EventKit Runtime 的完整只读快照。</summary>
        internal static EventKitWorkbenchSnapshot Create()
        {
            EventKitDiagnosticSnapshot diagnostics = EventKitDiagnosticRegistry.CreateSnapshot();
            var registrations = new List<EventKitRegistrationSnapshot>();
            AppendTypeRegistrations(registrations);
            AppendEnumRegistrations(registrations);
            AppendStringRegistrations(registrations);
            MergeActivities(registrations, diagnostics.Activities);
            registrations.Sort(CompareRegistrations);
            return new EventKitWorkbenchSnapshot(
                diagnostics.Version,
                diagnostics.Sequence,
                registrations,
                diagnostics.Activities);
        }

        /// <summary>追加 Type 总线中的当前事件容器。</summary>
        private static void AppendTypeRegistrations(List<EventKitRegistrationSnapshot> result)
        {
            IReadOnlyDictionary<Type, IEasyEvent> events = EventKit.Type.GetAllEvents();
            foreach (KeyValuePair<Type, IEasyEvent> pair in events)
            {
                string payloadType = ResolveContainerPayloadType(pair.Key);
                result.Add(CreateRegistration(
                    TYPE_CHANNEL,
                    payloadType,
                    payloadType,
                    GetListenerCount(pair.Value),
                    false));
            }
        }

        /// <summary>追加 Enum 总线中的当前事件容器。</summary>
        private static void AppendEnumRegistrations(List<EventKitRegistrationSnapshot> result)
        {
            IReadOnlyDictionary<EnumEventKey, EasyEvents> events = EventKit.Enum.GetAllEvents();
            foreach (KeyValuePair<EnumEventKey, EasyEvents> pair in events)
            {
                AppendEasyEvents(result, ENUM_CHANNEL, FormatEnumKey(pair.Key), pair.Value, false);
            }
        }

        /// <summary>追加 String 兼容总线中的当前事件容器。</summary>
        private static void AppendStringRegistrations(List<EventKitRegistrationSnapshot> result)
        {
#pragma warning disable CS0618
            IReadOnlyDictionary<string, EasyEvents> events = EventKit.String.GetAllEvents();
#pragma warning restore CS0618
            foreach (KeyValuePair<string, EasyEvents> pair in events)
            {
                AppendEasyEvents(result, STRING_CHANNEL, pair.Key, pair.Value, true);
            }
        }

        /// <summary>把同一个 Enum/String 键下的不同负载容器展开为独立行。</summary>
        private static void AppendEasyEvents(
            List<EventKitRegistrationSnapshot> result,
            string channel,
            string eventKey,
            EasyEvents events,
            bool deprecated)
        {
            IReadOnlyDictionary<Type, IEasyEvent> containers = events.GetAllEvents();
            if (containers.Count == 0)
            {
                result.Add(CreateRegistration(channel, eventKey, string.Empty, 0, deprecated));
                return;
            }

            foreach (KeyValuePair<Type, IEasyEvent> pair in containers)
            {
                result.Add(CreateRegistration(
                    channel,
                    eventKey,
                    ResolveContainerPayloadType(pair.Key),
                    GetListenerCount(pair.Value),
                    deprecated));
            }
        }

        /// <summary>创建一个尚未合并最近活动的注册行。</summary>
        private static EventKitRegistrationSnapshot CreateRegistration(
            string channel,
            string eventKey,
            string payloadType,
            int handlerCount,
            bool deprecated)
        {
            return new EventKitRegistrationSnapshot
            {
                Channel = channel,
                EventKey = eventKey ?? string.Empty,
                PayloadType = payloadType ?? string.Empty,
                HandlerCount = handlerCount,
                LastTimestampTicks = 0L,
                Deprecated = deprecated
            };
        }

        /// <summary>合并活动历史，并为没有监听器的纯发送事件创建可见行。</summary>
        private static void MergeActivities(
            List<EventKitRegistrationSnapshot> registrations,
            EventKitActivityRecord[] activities)
        {
            for (var index = 0; index < activities.Length; index++)
            {
                EventKitActivityRecord activity = activities[index];
                if (string.Equals(activity.Kind, "clear", StringComparison.Ordinal))
                {
                    ApplyClearActivity(registrations, activity);
                    continue;
                }

                EventKitRegistrationSnapshot registration = FindRegistration(registrations, activity);
                if (registration == null)
                {
                    registration = CreateRegistration(
                        activity.Channel,
                        activity.EventKey,
                        activity.PayloadType,
                        0,
                        string.Equals(activity.Channel, STRING_CHANNEL, StringComparison.Ordinal));
                    registrations.Add(registration);
                }

                registration.LastSequence = activity.Sequence;
                registration.LastTimestampTicks = activity.TimestampTicks;
            }
        }

        /// <summary>把指定键或整个通道的 clear 活动应用到全部受影响事件行。</summary>
        private static void ApplyClearActivity(
            List<EventKitRegistrationSnapshot> registrations,
            EventKitActivityRecord activity)
        {
            bool matched = false;
            for (var index = 0; index < registrations.Count; index++)
            {
                EventKitRegistrationSnapshot current = registrations[index];
                if (!string.Equals(current.Channel, activity.Channel, StringComparison.Ordinal)
                    || (!string.Equals(activity.EventKey, "*", StringComparison.Ordinal)
                        && !string.Equals(current.EventKey, activity.EventKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                current.LastSequence = activity.Sequence;
                current.LastTimestampTicks = activity.TimestampTicks;
                matched = true;
            }

            if (!matched && !string.Equals(activity.EventKey, "*", StringComparison.Ordinal))
            {
                EventKitRegistrationSnapshot row = CreateRegistration(
                    activity.Channel,
                    activity.EventKey,
                    string.Empty,
                    0,
                    string.Equals(activity.Channel, STRING_CHANNEL, StringComparison.Ordinal));
                row.LastSequence = activity.Sequence;
                row.LastTimestampTicks = activity.TimestampTicks;
                registrations.Add(row);
            }
        }

        /// <summary>优先按 channel/key/payload 精确查找，缺少 payload 时回落唯一事件键。</summary>
        private static EventKitRegistrationSnapshot FindRegistration(
            List<EventKitRegistrationSnapshot> registrations,
            EventKitActivityRecord activity)
        {
            EventKitRegistrationSnapshot fallback = null;
            bool fallbackAmbiguous = false;
            for (var index = 0; index < registrations.Count; index++)
            {
                EventKitRegistrationSnapshot current = registrations[index];
                if (!MatchesIdentity(current, activity.Channel, activity.EventKey))
                {
                    continue;
                }

                if (string.Equals(current.PayloadType, activity.PayloadType, StringComparison.Ordinal))
                {
                    return current;
                }

                if (fallback == null)
                {
                    fallback = current;
                }
                else
                {
                    fallbackAmbiguous = true;
                }
            }

            return fallbackAmbiguous ? null : fallback;
        }

        /// <summary>判断注册行是否匹配指定通道和事件键。</summary>
        private static bool MatchesIdentity(EventKitRegistrationSnapshot row, string channel, string eventKey)
        {
            return string.Equals(row.Channel, channel, StringComparison.Ordinal)
                && string.Equals(row.EventKey, eventKey, StringComparison.Ordinal);
        }

        /// <summary>从 EasyEvent 或 EasyEvent&lt;T&gt; 容器类型解析负载名。</summary>
        private static string ResolveContainerPayloadType(Type containerType)
        {
            if (containerType == null || !containerType.IsGenericType)
            {
                return string.Empty;
            }

            Type[] arguments = containerType.GetGenericArguments();
            return arguments.Length == 0 || arguments[0] == null
                ? string.Empty
                : EventKitTypeIdentity.Format(arguments[0]);
        }

        /// <summary>读取有效监听器数量，空容器按零处理。</summary>
        private static int GetListenerCount(IEasyEvent easyEvent)
        {
            return easyEvent == null ? 0 : easyEvent.ListenerCount;
        }

        /// <summary>把枚举类型和值格式化为稳定事件键。</summary>
        private static string FormatEnumKey(EnumEventKey key)
        {
            return EventKitTypeIdentity.FormatEnumEventKey(key);
        }

        /// <summary>按 channel、事件键和负载类型稳定排序。</summary>
        private static int CompareRegistrations(
            EventKitRegistrationSnapshot left,
            EventKitRegistrationSnapshot right)
        {
            int channel = GetChannelRank(left.Channel).CompareTo(GetChannelRank(right.Channel));
            if (channel != 0)
            {
                return channel;
            }

            int eventKey = string.Compare(left.EventKey, right.EventKey, StringComparison.Ordinal);
            return eventKey != 0
                ? eventKey
                : string.Compare(left.PayloadType, right.PayloadType, StringComparison.Ordinal);
        }

        /// <summary>返回稳定的 Runtime channel 排序权重。</summary>
        private static int GetChannelRank(string channel)
        {
            if (string.Equals(channel, TYPE_CHANNEL, StringComparison.Ordinal)) return 0;
            if (string.Equals(channel, ENUM_CHANNEL, StringComparison.Ordinal)) return 1;
            if (string.Equals(channel, STRING_CHANNEL, StringComparison.Ordinal)) return 2;
            return 3;
        }
    }
}
#endif
