#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 把 EventKit Runtime 快照写成稳定 JSON，不依赖 Unity 或 Godot 序列化器。
    /// </summary>
    internal static class EventKitJsonWriter
    {
        /// <summary>创建 EventKit Workbench state payload。</summary>
        internal static string WriteWorkbench(EventKitWorkbenchSnapshot snapshot)
        {
            var builder = new StringBuilder(1024);
            builder.Append("{\"version\":").Append(snapshot.Version);
            builder.Append(",\"sequence\":").Append(snapshot.Sequence);
            AppendCounts(builder, snapshot.Registrations, snapshot.Activities.Length);
            AppendRegistrations(builder, snapshot.Registrations);
            AppendActivities(builder, snapshot.Activities);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>追加 Runtime 事件、监听器和近期活动统计。</summary>
        private static void AppendCounts(
            StringBuilder builder,
            IReadOnlyList<EventKitRegistrationSnapshot> registrations,
            int activityCount)
        {
            int typeCount = 0;
            int enumCount = 0;
            int stringCount = 0;
            int handlerCount = 0;
            CountRegistrations(registrations, ref typeCount, ref enumCount, ref stringCount, ref handlerCount);
            builder.Append(",\"counts\":{\"typeEvents\":").Append(typeCount);
            builder.Append(",\"enumEvents\":").Append(enumCount);
            builder.Append(",\"stringEvents\":").Append(stringCount);
            builder.Append(",\"totalEvents\":").Append(registrations.Count);
            builder.Append(",\"totalHandlers\":").Append(handlerCount);
            builder.Append(",\"recentActivities\":").Append(activityCount).Append('}');
        }

        /// <summary>统计各 Runtime channel 行数和监听器总数。</summary>
        private static void CountRegistrations(
            IReadOnlyList<EventKitRegistrationSnapshot> registrations,
            ref int typeCount,
            ref int enumCount,
            ref int stringCount,
            ref int handlerCount)
        {
            for (var index = 0; index < registrations.Count; index++)
            {
                EventKitRegistrationSnapshot row = registrations[index];
                if (string.Equals(row.Channel, "Type", StringComparison.Ordinal)) typeCount++;
                else if (string.Equals(row.Channel, "Enum", StringComparison.Ordinal)) enumCount++;
                else if (string.Equals(row.Channel, "String", StringComparison.Ordinal)) stringCount++;
                handlerCount += row.HandlerCount;
            }
        }

        /// <summary>追加当前 Runtime 注册与纯发送事件行。</summary>
        private static void AppendRegistrations(
            StringBuilder builder,
            IReadOnlyList<EventKitRegistrationSnapshot> registrations)
        {
            builder.Append(",\"events\":[");
            for (var index = 0; index < registrations.Count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendRegistration(builder, registrations[index]);
            }

            builder.Append(']');
        }

        /// <summary>追加单个事件注册行。</summary>
        private static void AppendRegistration(StringBuilder builder, EventKitRegistrationSnapshot row)
        {
            builder.Append("{\"channel\":");
            AppendString(builder, row.Channel);
            builder.Append(",\"eventKey\":");
            AppendString(builder, row.EventKey);
            builder.Append(",\"payloadType\":");
            AppendString(builder, row.PayloadType);
            builder.Append(",\"handlerCount\":").Append(row.HandlerCount);
            builder.Append(",\"lastSequence\":").Append(row.LastSequence);
            builder.Append(",\"lastTime\":");
            AppendTime(builder, row.LastTimestampTicks);
            builder.Append(",\"deprecated\":").Append(row.Deprecated ? "true" : "false");
            builder.Append('}');
        }

        /// <summary>追加有界近期活动对象。</summary>
        private static void AppendActivities(StringBuilder builder, EventKitActivityRecord[] activities)
        {
            builder.Append(",\"recentEvents\":{\"count\":").Append(activities.Length);
            builder.Append(",\"events\":[");
            for (var index = 0; index < activities.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendActivity(builder, activities[index]);
            }

            builder.Append("]}");
        }

        /// <summary>追加一条带全局 sequence 的活动记录。</summary>
        private static void AppendActivity(StringBuilder builder, EventKitActivityRecord activity)
        {
            builder.Append("{\"sequence\":").Append(activity.Sequence);
            builder.Append(",\"kind\":");
            AppendString(builder, activity.Kind);
            builder.Append(",\"channel\":");
            AppendString(builder, activity.Channel);
            builder.Append(",\"eventKey\":");
            AppendString(builder, activity.EventKey);
            builder.Append(",\"payloadType\":");
            AppendString(builder, activity.PayloadType);
            builder.Append(",\"handler\":");
            AppendString(builder, activity.Handler);
            builder.Append(",\"time\":");
            AppendTime(builder, activity.TimestampTicks);
            builder.Append('}');
        }

        /// <summary>直接追加 HH:mm:ss.fff，避免快照构建为每条活动创建时间字符串。</summary>
        private static void AppendTime(StringBuilder builder, long timestampTicks)
        {
            builder.Append('"');
            if (timestampTicks <= 0L)
            {
                builder.Append('"');
                return;
            }

            long timeTicks = timestampTicks % TimeSpan.TicksPerDay;
            int hours = (int)(timeTicks / TimeSpan.TicksPerHour);
            int minutes = (int)(timeTicks / TimeSpan.TicksPerMinute % 60L);
            int seconds = (int)(timeTicks / TimeSpan.TicksPerSecond % 60L);
            int milliseconds = (int)(timeTicks / TimeSpan.TicksPerMillisecond % 1000L);
            AppendTwoDigits(builder, hours);
            builder.Append(':');
            AppendTwoDigits(builder, minutes);
            builder.Append(':');
            AppendTwoDigits(builder, seconds);
            builder.Append('.');
            AppendThreeDigits(builder, milliseconds);
            builder.Append('"');
        }

        /// <summary>追加固定两位十进制数字。</summary>
        private static void AppendTwoDigits(StringBuilder builder, int value)
        {
            builder.Append((char)('0' + value / 10));
            builder.Append((char)('0' + value % 10));
        }

        /// <summary>追加固定三位十进制数字。</summary>
        private static void AppendThreeDigits(StringBuilder builder, int value)
        {
            builder.Append((char)('0' + value / 100));
            builder.Append((char)('0' + value / 10 % 10));
            builder.Append((char)('0' + value % 10));
        }

        /// <summary>追加经过统一转义的 JSON 字符串。</summary>
        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            builder.Append(JsonHelper.EscapeString(value ?? string.Empty));
            builder.Append('"');
        }
    }
}
#endif
