#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时视图的数据收集与触发处理逻辑。
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 数据收集

        /// <summary>
        /// 收集当前所有活跃事件。
        /// </summary>
        private void CollectEventInfos()
        {
            mEventInfos.Clear();
            CollectEnumEvents();
            CollectTypeEvents();
            CollectStringEvents();
        }

        /// <summary>
        /// 收集 Enum 事件。
        /// </summary>
        private void CollectEnumEvents()
        {
            var enumEvents = EventKit.Enum.GetAllEvents();
            foreach (var kvp in enumEvents)
            {
                foreach (var innerKvp in kvp.Value.GetAllEvents())
                {
                    if (innerKvp.Value.ListenerCount == 0)
                    {
                        continue;
                    }

                    var enumName = Enum.GetName(kvp.Key.EnumType, kvp.Key.EnumValue)
                                   ?? kvp.Key.EnumValue.ToString();
                    var paramTypeName = ExtractParamTypeName(innerKvp.Key);
                    var cacheKey = $"Enum_{kvp.Key.EnumType.Name}.{enumName}";

                    mEventInfos.Add(new EventInfo
                    {
                        EventType = "Enum",
                        EventKey = $"{kvp.Key.EnumType.Name}.{enumName}",
                        ParamType = paramTypeName,
                        ListenerCount = innerKvp.Value.ListenerCount,
                        Event = innerKvp.Value,
                        LastTriggerTime = GetLastTriggerTime(cacheKey),
                        TriggerCount = GetTriggerCount(cacheKey)
                    });
                }
            }
        }

        /// <summary>
        /// 收集 Type 事件。
        /// </summary>
        private void CollectTypeEvents()
        {
            var typeEvents = EventKit.Type.GetAllEvents();
            foreach (var kvp in typeEvents)
            {
                if (kvp.Value.ListenerCount == 0)
                {
                    continue;
                }

                var eventWrapperType = kvp.Key;
                string actualTypeName = eventWrapperType.IsGenericType
                    ? eventWrapperType.GetGenericArguments()[0]?.Name ?? eventWrapperType.Name
                    : eventWrapperType.Name;

                var cacheKey = $"Type_{actualTypeName}";
                mEventInfos.Add(new EventInfo
                {
                    EventType = "Type",
                    EventKey = actualTypeName,
                    ParamType = actualTypeName,
                    ListenerCount = kvp.Value.ListenerCount,
                    Event = kvp.Value,
                    LastTriggerTime = GetLastTriggerTime(cacheKey),
                    TriggerCount = GetTriggerCount(cacheKey)
                });
            }
        }

        /// <summary>
        /// 收集 String 事件。
        /// </summary>
        private void CollectStringEvents()
        {
#pragma warning disable CS0612, CS0618
            var stringEvents = EventKit.String.GetAllEvents();
#pragma warning restore CS0612, CS0618
            foreach (var kvp in stringEvents)
            {
                foreach (var innerKvp in kvp.Value.GetAllEvents())
                {
                    if (innerKvp.Value.ListenerCount == 0)
                    {
                        continue;
                    }

                    var paramTypeName = ExtractParamTypeName(innerKvp.Key);
                    var cacheKey = $"String_{kvp.Key}";

                    mEventInfos.Add(new EventInfo
                    {
                        EventType = "String",
                        EventKey = $"\"{kvp.Key}\"",
                        ParamType = paramTypeName,
                        ListenerCount = innerKvp.Value.ListenerCount,
                        Event = innerKvp.Value,
                        LastTriggerTime = GetLastTriggerTime(cacheKey),
                        TriggerCount = GetTriggerCount(cacheKey)
                    });
                }
            }
        }

        /// <summary>
        /// 从事件包装类型中提取参数类型名。
        /// </summary>
        private static string ExtractParamTypeName(Type eventWrapperType)
        {
            if (!eventWrapperType.IsGenericType)
            {
                return "void";
            }

            var genericArgs = eventWrapperType.GetGenericArguments();
            return genericArgs.Length > 0 ? genericArgs[0].Name : "void";
        }

        /// <summary>
        /// 获取事件最后一次触发时间。
        /// </summary>
        private double GetLastTriggerTime(string key)
            => mTriggerTimeCache.TryGetValue(key, out var time) ? time : 0;

        /// <summary>
        /// 获取事件累计触发次数。
        /// </summary>
        private int GetTriggerCount(string key)
            => mTriggerCountCache.TryGetValue(key, out var count) ? count : 0;

        #endregion

        #region 触发处理

        /// <summary>
        /// 处理运行时事件触发通知，并同步刷新缓存、动画和时间轴。
        /// </summary>
        private void OnEventTriggered((string eventType, string eventKey, string args) data)
        {
            var cacheKey = $"{data.eventType}_{data.eventKey}";
            var now = EditorApplication.timeSinceStartup;

            mTriggerTimeCache[cacheKey] = now;
            mTriggerCountCache.TryGetValue(cacheKey, out var count);
            mTriggerCountCache[cacheKey] = count + 1;

            PlayEventTriggerAnimation(data.eventType, data.eventKey);

            if (UpdateEventInfoOnTrigger(data.eventType, data.eventKey, now, count + 1))
            {
                UpdateDetailPanelStats();
                AddTimelineEntry(data.args);
            }
        }

        /// <summary>
        /// 更新事件缓存中的触发统计，并返回是否需要刷新右侧详情。
        /// </summary>
        private bool UpdateEventInfoOnTrigger(string eventType, string eventKey, double now, int triggerCount)
        {
            foreach (var info in mEventInfos)
            {
                if (info.EventType != eventType || info.EventKey != eventKey)
                {
                    continue;
                }

                info.LastTriggerTime = now;
                info.TriggerCount = triggerCount;

                if (mSelectedEvent?.EventType == info.EventType && mSelectedEvent?.EventKey == info.EventKey)
                {
                    mSelectedEvent.LastTriggerTime = now;
                    mSelectedEvent.TriggerCount = triggerCount;
                    return true;
                }

                return false;
            }

            if (mSelectedEvent?.EventType == eventType && mSelectedEvent?.EventKey == eventKey)
            {
                mSelectedEvent.LastTriggerTime = now;
                mSelectedEvent.TriggerCount = triggerCount;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 仅刷新详情面板中的统计数字。
        /// </summary>
        private void UpdateDetailPanelStats()
        {
            if (mSelectedEvent == null || mDetailTriggerCount == null)
            {
                return;
            }

            mDetailTriggerCount.text = $"{mSelectedEvent.TriggerCount} 次";
            mDetailListenerCount.text = $"{mSelectedEvent.ListenerCount} 个";
        }

        /// <summary>
        /// 向时间轴插入一条新的触发记录。
        /// </summary>
        private void AddTimelineEntry(string args)
        {
            if (mSelectedEvent == null || mTimelineContainer == null)
            {
                return;
            }

            var cacheKey = $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}";
            if (!mTimelineHistoryCache.TryGetValue(cacheKey, out var history))
            {
                history = new List<TimelineEntry>(MAX_TIMELINE_ENTRIES);
                mTimelineHistoryCache[cacheKey] = history;
            }

            var entry = new TimelineEntry { Time = Time.time, Args = args };
            history.Insert(0, entry);

            while (history.Count > MAX_TIMELINE_ENTRIES)
            {
                history.RemoveAt(history.Count - 1);
            }

            mTimelineContainer.Q<VisualElement>("empty-timeline-state")?.RemoveFromHierarchy();
            mTimelineContainer.Insert(0, CreateTimelineEntry("Send", $"{entry.Time:F2}s", args));

            while (mTimelineContainer.childCount > MAX_TIMELINE_ENTRIES)
            {
                mTimelineContainer.RemoveAt(mTimelineContainer.childCount - 1);
            }
        }

        #endregion
    }
}
#endif
