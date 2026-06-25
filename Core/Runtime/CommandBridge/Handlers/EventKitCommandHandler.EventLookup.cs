using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 命令处理器的事件详情查询逻辑。
    /// </summary>
    public sealed partial class EventKitCommandHandler
    {
        private static string GetEvent(string payloadJson)
        {
            var channel = NormalizeChannel(JsonHelper.ExtractString(payloadJson, "channel") ??
                                           JsonHelper.ExtractString(payloadJson, "eventType"));
            var eventKey = JsonHelper.ExtractString(payloadJson, "eventKey") ??
                           JsonHelper.ExtractString(payloadJson, "key") ??
                           JsonHelper.ExtractString(payloadJson, "eventName");
            var payloadType = JsonHelper.ExtractString(payloadJson, "payloadType") ??
                              JsonHelper.ExtractString(payloadJson, "parameterType");

            if (string.IsNullOrEmpty(eventKey))
                throw new ArgumentException("Missing 'eventKey' in payload");

            var typeEvents = EventKit.Type.GetAllEvents();
            var enumEvents = EventKit.Enum.GetAllEvents();
#pragma warning disable CS0618
            var stringEvents = EventKit.String.GetAllEvents();
#pragma warning restore CS0618

            var sb = new StringBuilder(256);
            sb.Append("{\"query\":{\"channel\":\"");
            sb.Append(JsonHelper.EscapeString(channel));
            sb.Append("\",\"eventKey\":\"");
            sb.Append(JsonHelper.EscapeString(eventKey));
            sb.Append("\",\"payloadType\":\"");
            sb.Append(JsonHelper.EscapeString(payloadType));
            sb.Append("\"},\"matches\":[");

            var first = true;
            var count = 0;
            AppendMatchingTypeRows(sb, ref first, ref count, channel, eventKey, payloadType, typeEvents);
            AppendMatchingEnumRows(sb, ref first, ref count, channel, eventKey, payloadType, enumEvents);
            AppendMatchingStringRows(sb, ref first, ref count, channel, eventKey, payloadType, stringEvents);

            sb.Append("],\"count\":");
            sb.Append(count);
            sb.Append(",\"recentEvents\":");
            AppendRecentEventsJson(sb);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendMatchingTypeRows(
            StringBuilder sb,
            ref bool first,
            ref int count,
            string channel,
            string eventKey,
            string payloadType,
            IReadOnlyDictionary<Type, IEasyEvent> events)
        {
            if (!MatchesQuery(channel, "Type"))
                return;

            foreach (var kvp in events)
            {
                var key = FormatTypeEventKey(kvp.Key);
                if (!StringEquals(key, eventKey) || !MatchesPayload(payloadType, key))
                    continue;

                var handlerCount = kvp.Value != null ? kvp.Value.ListenerCount : 0;
                AppendEventRow(sb, ref first, "Type", key, key, key, false, handlerCount);
                count++;
            }
        }

        private static void AppendMatchingEnumRows(
            StringBuilder sb,
            ref bool first,
            ref int count,
            string channel,
            string eventKey,
            string payloadType,
            IReadOnlyDictionary<EnumEventKey, EasyEvents> events)
        {
            if (!MatchesQuery(channel, "Enum"))
                return;

            foreach (var kvp in events)
            {
                var key = FormatEnumKey(kvp.Key);
                if (!StringEquals(key, eventKey))
                    continue;

                AppendMatchingEasyEventRows(sb, ref first, ref count, "Enum", key,
                    kvp.Key.EnumType != null ? kvp.Key.EnumType.Name : "Enum", false, payloadType, kvp.Value);
            }
        }

        private static void AppendMatchingStringRows(
            StringBuilder sb,
            ref bool first,
            ref int count,
            string channel,
            string eventKey,
            string payloadType,
            IReadOnlyDictionary<string, EasyEvents> events)
        {
            if (!MatchesQuery(channel, "String"))
                return;

            foreach (var kvp in events)
            {
                if (!StringEquals(kvp.Key, eventKey))
                    continue;

                AppendMatchingEasyEventRows(sb, ref first, ref count, "String", kvp.Key,
                    "String", true, payloadType, kvp.Value);
            }
        }

        private static void AppendMatchingEasyEventRows(
            StringBuilder sb,
            ref bool first,
            ref int count,
            string channel,
            string key,
            string type,
            bool deprecated,
            string payloadType,
            EasyEvents events)
        {
            if (events == null)
            {
                if (!MatchesPayload(payloadType, string.Empty))
                    return;

                AppendEventRow(sb, ref first, channel, key, type, string.Empty, deprecated, 0);
                count++;
                return;
            }

            var typedEvents = events.GetAllEvents();
            if (typedEvents.Count == 0)
            {
                if (!MatchesPayload(payloadType, string.Empty))
                    return;

                AppendEventRow(sb, ref first, channel, key, type, string.Empty, deprecated, 0);
                count++;
                return;
            }

            foreach (var kvp in typedEvents)
            {
                var rowPayloadType = FormatPayloadType(kvp.Key);
                if (!MatchesPayload(payloadType, rowPayloadType))
                    continue;

                var handlerCount = kvp.Value != null ? kvp.Value.ListenerCount : 0;
                AppendEventRow(sb, ref first, channel, key, type, rowPayloadType, deprecated, handlerCount);
                count++;
            }
        }

        private static void AppendDiagnosticsJson(
            StringBuilder sb,
            int typeEventCount,
            int enumEventCount,
            int stringEventCount,
            int runtimeListenerCount)
        {
            sb.Append("{\"typeEventCount\":");
            sb.Append(typeEventCount);
            sb.Append(",\"enumEventCount\":");
            sb.Append(enumEventCount);
            sb.Append(",\"stringEventCount\":");
            sb.Append(stringEventCount);
            sb.Append(",\"runtimeListenerCount\":");
            sb.Append(runtimeListenerCount);
            sb.Append(",\"runtimeListenerSource\":\"EventKit runtime registration dictionaries\"");
            sb.Append(",\"editorBridge\":\"UNITY_EDITOR/GODOT hooks observe registrations and publish snapshots only\"}");
        }

        private static string NormalizeChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return string.Empty;

            if (StringEquals(channel, "Type"))
                return "Type";
            if (StringEquals(channel, "Enum"))
                return "Enum";
            if (StringEquals(channel, "String"))
                return "String";

            return channel.Trim();
        }

        private static bool MatchesQuery(string queryChannel, string rowChannel)
        {
            return string.IsNullOrEmpty(queryChannel) || StringEquals(queryChannel, rowChannel);
        }

        private static bool MatchesPayload(string queryPayloadType, string rowPayloadType)
        {
            return string.IsNullOrEmpty(queryPayloadType) || StringEquals(queryPayloadType, rowPayloadType);
        }

        private static bool StringEquals(string a, string b)
        {
            return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
