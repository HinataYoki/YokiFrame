using System;
using System.Collections.Generic;
using System.Text;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 命令处理器：查询注册表、触发事件和启动监控。
    /// </summary>
    public sealed partial class EventKitCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName => "EventKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "list_registrations",
            "get_workbench_snapshot",
            "get_event",
            "get_recent_events",
            "fire_event",
            "monitor_start"
        };

        /// <summary>
        /// 由 Adapter 注入的 EventKit 最近活动记录。Base 只拥有注册表，运行时轨迹采集由 Adapter 负责。
        /// </summary>
        public static Func<string> RecentEventsProvider;

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "list_registrations":
                    return ListRegistrations();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot(payloadJson);
                case "get_event":
                    return GetEvent(payloadJson);
                case "get_recent_events":
                    return GetRecentEvents(payloadJson);
                case "fire_event":
                    return FireEvent(payloadJson);
                case "monitor_start":
                    return StartMonitor(payloadJson);
                default:
                    throw new NotSupportedException($"Unknown EventKit action '{action}'");
            }
        }

        private static string ListRegistrations()
        {
            return BuildRegistrationsJson(false);
        }

        private static string GetWorkbenchSnapshot(string payloadJson)
        {
            return BuildRegistrationsJson(true);
        }

        private static string BuildRegistrationsJson(bool includeDiagnostics)
        {
            var typeEvents = EventKit.Type.GetAllEvents();
            var enumEvents = EventKit.Enum.GetAllEvents();
#pragma warning disable CS0618
            var stringEvents = EventKit.String.GetAllEvents();
#pragma warning restore CS0618

            var typeHandlerCount = CountTypeHandlers(typeEvents);
            var enumHandlerCount = CountEnumHandlers(enumEvents);
            var stringHandlerCount = CountStringHandlers(stringEvents);

            var sb = new StringBuilder(512);
            sb.Append("{\"counts\":{\"typeEvents\":");
            sb.Append(typeEvents.Count);
            sb.Append(",\"enumEvents\":");
            sb.Append(enumEvents.Count);
            sb.Append(",\"stringEvents\":");
            sb.Append(stringEvents.Count);
            sb.Append(",\"totalEvents\":");
            sb.Append(typeEvents.Count + enumEvents.Count + stringEvents.Count);
            sb.Append(",\"totalHandlers\":");
            sb.Append(typeHandlerCount + enumHandlerCount + stringHandlerCount);
            sb.Append("},\"registrations\":{\"typeEvents\":");
            AppendTypeEventRows(sb, typeEvents);
            sb.Append(",\"enumEvents\":");
            AppendEnumEventRows(sb, enumEvents);
            sb.Append(",\"stringEvents\":");
            AppendStringEventRows(sb, stringEvents);
            sb.Append("},\"recentEvents\":");
            AppendRecentEventsJson(sb);
            if (includeDiagnostics)
            {
                sb.Append(",\"diagnostics\":");
                AppendDiagnosticsJson(sb, typeEvents.Count, enumEvents.Count, stringEvents.Count,
                    typeHandlerCount + enumHandlerCount + stringHandlerCount);
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static int CountTypeHandlers(IReadOnlyDictionary<Type, IEasyEvent> events)
        {
            var count = 0;
            foreach (var kvp in events)
                count += kvp.Value != null ? kvp.Value.ListenerCount : 0;

            return count;
        }

        private static int CountEnumHandlers(IReadOnlyDictionary<EnumEventKey, EasyEvents> events)
        {
            var count = 0;
            foreach (var kvp in events)
                count += CountEasyEventsHandlers(kvp.Value);

            return count;
        }

        private static int CountStringHandlers(IReadOnlyDictionary<string, EasyEvents> events)
        {
            var count = 0;
            foreach (var kvp in events)
                count += CountEasyEventsHandlers(kvp.Value);

            return count;
        }

        private static int CountEasyEventsHandlers(EasyEvents events)
        {
            if (events == null)
                return 0;

            var count = 0;
            foreach (var kvp in events.GetAllEvents())
                count += kvp.Value != null ? kvp.Value.ListenerCount : 0;

            return count;
        }

        private static void AppendTypeEventRows(StringBuilder sb, IReadOnlyDictionary<Type, IEasyEvent> events)
        {
            sb.Append('[');
            var first = true;
            foreach (var kvp in events)
            {
                if (!first)
                    sb.Append(',');

                var key = FormatTypeEventKey(kvp.Key);
                var handlerCount = kvp.Value != null ? kvp.Value.ListenerCount : 0;
                sb.Append("{\"channel\":\"Type\",\"key\":\"");
                sb.Append(JsonHelper.EscapeString(key));
                sb.Append("\",\"type\":\"");
                sb.Append(JsonHelper.EscapeString(key));
                sb.Append("\",\"payloadType\":\"");
                sb.Append(JsonHelper.EscapeString(key));
                sb.Append("\",\"handlerCount\":");
                sb.Append(handlerCount);
                sb.Append('}');
                first = false;
            }
            sb.Append(']');
        }

        private static string FormatTypeEventKey(Type eventContainerType)
        {
            if (eventContainerType == null)
                return "Unknown";

            if (eventContainerType.IsGenericType)
            {
                var arguments = eventContainerType.GetGenericArguments();
                if (arguments.Length > 0 && arguments[0] != null)
                    return arguments[0].Name;
            }

            return eventContainerType.Name;
        }

        private static void AppendEnumEventRows(StringBuilder sb, IReadOnlyDictionary<EnumEventKey, EasyEvents> events)
        {
            sb.Append('[');
            var first = true;
            foreach (var kvp in events)
            {
                AppendEnumOrStringRows(
                    sb,
                    ref first,
                    "Enum",
                    FormatEnumKey(kvp.Key),
                    kvp.Key.EnumType != null ? kvp.Key.EnumType.Name : "Enum",
                    false,
                    kvp.Value);
            }
            sb.Append(']');
        }

        private static void AppendStringEventRows(StringBuilder sb, IReadOnlyDictionary<string, EasyEvents> events)
        {
            sb.Append('[');
            var first = true;
            foreach (var kvp in events)
            {
                AppendEnumOrStringRows(sb, ref first, "String", kvp.Key, "String", true, kvp.Value);
            }
            sb.Append(']');
        }

        private static void AppendEnumOrStringRows(
            StringBuilder sb,
            ref bool first,
            string channel,
            string key,
            string type,
            bool deprecated,
            EasyEvents events)
        {
            if (events == null)
            {
                AppendEventRow(sb, ref first, channel, key, type, string.Empty, deprecated, 0);
                return;
            }

            var typedEvents = events.GetAllEvents();
            if (typedEvents.Count == 0)
            {
                AppendEventRow(sb, ref first, channel, key, type, string.Empty, deprecated, 0);
                return;
            }

            foreach (var kvp in typedEvents)
            {
                var payloadType = FormatPayloadType(kvp.Key);
                var handlerCount = kvp.Value != null ? kvp.Value.ListenerCount : 0;
                AppendEventRow(sb, ref first, channel, key, type, payloadType, deprecated, handlerCount);
            }
        }

        private static void AppendEventRow(
            StringBuilder sb,
            ref bool first,
            string channel,
            string key,
            string type,
            string payloadType,
            bool deprecated,
            int handlerCount)
        {
            if (!first)
                sb.Append(',');

            sb.Append("{\"channel\":\"");
            sb.Append(JsonHelper.EscapeString(channel));
            sb.Append("\",\"key\":\"");
            sb.Append(JsonHelper.EscapeString(key));
            sb.Append("\",\"type\":\"");
            sb.Append(JsonHelper.EscapeString(type));
            sb.Append("\",\"payloadType\":\"");
            sb.Append(JsonHelper.EscapeString(payloadType));
            sb.Append("\",\"deprecated\":");
            sb.Append(deprecated ? "true" : "false");
            sb.Append(",\"handlerCount\":");
            sb.Append(handlerCount);
            sb.Append('}');
            first = false;
        }

        private static string FormatPayloadType(Type eventContainerType)
        {
            if (eventContainerType == null)
                return string.Empty;

            if (!eventContainerType.IsGenericType)
                return string.Empty;

            var arguments = eventContainerType.GetGenericArguments();
            if (arguments.Length <= 0 || arguments[0] == null)
                return string.Empty;

            return arguments[0].Name;
        }

        private static string FormatEnumKey(EnumEventKey key)
        {
            var typeName = key.EnumType != null ? key.EnumType.Name : "Enum";
            var valueName = key.EnumValue.ToString();
            if (key.EnumType != null)
            {
                try
                {
                    var name = Enum.GetName(key.EnumType, key.EnumValue);
                    if (!string.IsNullOrEmpty(name))
                        valueName = name;
                }
                catch
                {
                    valueName = key.EnumValue.ToString();
                }
            }

            return typeName + "." + valueName;
        }

        private static void AppendRecentEventsJson(StringBuilder sb)
        {
            var provider = RecentEventsProvider;
            if (provider != null)
            {
                var json = provider();
                if (!string.IsNullOrEmpty(json))
                {
                    sb.Append(json);
                    return;
                }
            }

            sb.Append("{\"events\":[],\"count\":0}");
        }

        private static string GetRecentEvents(string payloadJson)
        {
            var sb = new StringBuilder(256);
            AppendRecentEventsJson(sb);
            return sb.ToString();
        }

        private static string FireEvent(string payloadJson)
        {
            var eventType = JsonHelper.ExtractString(payloadJson, "eventType") ?? "Type";
            var eventName = JsonHelper.ExtractString(payloadJson, "eventName");

            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException("Missing 'eventName' in payload");

            // 命令桥目前只支持受控发送无参数事件；带类型解析的发送交给 Adapter 层扩展。
            switch (eventType)
            {
                case "Type":
                    // Base 层不能动态实例化宿主类型，这里只返回说明信息。
                    return "{\"fired\":false,\"message\":\"Type events require a known type at compile time. Use Enum events for scriptable control.\"}";
                case "Enum":
                    return "{\"fired\":false,\"message\":\"Enum event firing through command bridge requires Adapter-level enum resolution.\"}";
                default:
                    return "{\"fired\":false,\"message\":\"Event type '" + eventType + "' not supported through command bridge.\"}";
            }
        }

        private static string StartMonitor(string payloadJson)
        {
            // 监控是 Adapter 层能力；Base 只提供注册 hook。
            return "{\"monitoring\":true,\"message\":\"EventKit monitoring started (Adapter-level hooks required for persistence)\"}";
        }
    }
}
