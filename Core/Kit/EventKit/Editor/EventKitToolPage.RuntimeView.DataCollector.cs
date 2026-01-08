#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时视图 - 数据收集与事件处理
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 数据收集

        /// <summary>
        /// 收集所有事件信息
        /// </summary>
        private void CollectEventInfos()
        {
            mEventInfos.Clear();
            CollectEnumEvents();
            CollectTypeEvents();
            CollectStringEvents();
        }

        /// <summary>
        /// 收集 Enum 事件
        /// </summary>
        private void CollectEnumEvents()
        {
            var enumEvents = EventKit.Enum.GetAllEvents();
            foreach (var kvp in enumEvents)
            {
                foreach (var innerKvp in kvp.Value.GetAllEvents())
                {
                    if (innerKvp.Value.ListenerCount == 0) continue;

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
        /// 收集 Type 事件
        /// </summary>
        private void CollectTypeEvents()
        {
            var typeEvents = EventKit.Type.GetAllEvents();
            foreach (var kvp in typeEvents)
            {
                if (kvp.Value.ListenerCount == 0) continue;

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
        /// 收集 String 事件（已过时）
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
                    if (innerKvp.Value.ListenerCount == 0) continue;

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
        /// 从事件包装类型提取参数类型名
        /// </summary>
        private static string ExtractParamTypeName(Type eventWrapperType)
        {
            if (!eventWrapperType.IsGenericType) return "void";
            var genericArgs = eventWrapperType.GetGenericArguments();
            return genericArgs.Length > 0 ? genericArgs[0].Name : "void";
        }

        private double GetLastTriggerTime(string key) 
            => mTriggerTimeCache.TryGetValue(key, out var time) ? time : 0;
        
        private int GetTriggerCount(string key)
            => mTriggerCountCache.TryGetValue(key, out var count) ? count : 0;

        #endregion

        #region 事件触发处理

        /// <summary>
        /// 处理事件触发通知，更新触发时间缓存并播放动画
        /// </summary>
        private void OnEventTriggered((string eventType, string eventKey, string args) data)
        {
            var cacheKey = $"{data.eventType}_{data.eventKey}";
            var now = EditorApplication.timeSinceStartup;
            
            // 更新缓存
            mTriggerTimeCache[cacheKey] = now;
            mTriggerCountCache.TryGetValue(cacheKey, out var count);
            mTriggerCountCache[cacheKey] = count + 1;
            
            // 播放泳道动画
            PlayEventTriggerAnimation(data.eventType, data.eventKey);
            
            // 同步更新 EventInfo 并检查是否需要更新时间轴
            if (UpdateEventInfoOnTrigger(data.eventType, data.eventKey, now, count + 1))
            {
                UpdateDetailPanelStats();
                AddTimelineEntry(data.args);
            }
        }

        /// <summary>
        /// 更新 EventInfo 数据，返回是否需要更新时间轴
        /// </summary>
        private bool UpdateEventInfoOnTrigger(string eventType, string eventKey, double now, int triggerCount)
        {
            foreach (var info in mEventInfos)
            {
                if (info.EventType != eventType || info.EventKey != eventKey) continue;
                
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

            // 检查选中事件是否匹配
            if (mSelectedEvent?.EventType == eventType && mSelectedEvent?.EventKey == eventKey)
            {
                mSelectedEvent.LastTriggerTime = now;
                mSelectedEvent.TriggerCount = triggerCount;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 仅更新详情面板的统计数字
        /// </summary>
        private void UpdateDetailPanelStats()
        {
            if (mSelectedEvent == null || mDetailTriggerCount == null) return;
            mDetailTriggerCount.text = $"{mSelectedEvent.TriggerCount} 次";
            mDetailListenerCount.text = $"{mSelectedEvent.ListenerCount} 个";
        }
        
        /// <summary>
        /// 添加时间轴条目
        /// </summary>
        private void AddTimelineEntry(string args)
        {
            if (mSelectedEvent == null || mTimelineContainer == null) return;
            
            var cacheKey = $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}";
            if (!mTimelineHistoryCache.TryGetValue(cacheKey, out var history))
            {
                history = new List<TimelineEntry>(MAX_TIMELINE_ENTRIES);
                mTimelineHistoryCache[cacheKey] = history;
            }
            
            var entry = new TimelineEntry { Time = Time.time, Args = args };
            history.Insert(0, entry);
            
            // 限制数量
            while (history.Count > MAX_TIMELINE_ENTRIES)
                history.RemoveAt(history.Count - 1);
            
            // 移除空状态提示
            mTimelineContainer.Q<VisualElement>("empty-timeline-state")?.RemoveFromHierarchy();
            
            // 插入新条目
            mTimelineContainer.Insert(0, CreateTimelineEntry("Send", $"{entry.Time:F2}s", args));
            
            // 限制 UI 条目数量
            while (mTimelineContainer.childCount > MAX_TIMELINE_ENTRIES)
                mTimelineContainer.RemoveAt(mTimelineContainer.childCount - 1);
        }

        #endregion
    }
}
#endif
