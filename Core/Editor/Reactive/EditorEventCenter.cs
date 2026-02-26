#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 编辑器专用事件中心 - 不依赖运行时 EventKit
    /// 特点：类型安全、零 GC、自动清理
    /// </summary>
    public static class EditorEventCenter
    {
        #region 内部数据结构

        /// <summary>
        /// 订阅信息 - 记录订阅者和回调
        /// </summary>
        private sealed class Subscription
        {
            public object Owner;
            public Delegate Handler;
            public int Id;
        }

        /// <summary>
        /// 事件通道 - 管理同一类型的所有订阅
        /// </summary>
        private sealed class EventChannel
        {
            public readonly List<Subscription> Subscriptions = new(8);
            public readonly Stack<Subscription> Pool = new(4);
            public int NextId;

            public Subscription GetSubscription()
            {
                return Pool.Count > 0 ? Pool.Pop() : new Subscription();
            }

            public void ReturnSubscription(Subscription sub)
            {
                sub.Owner = null;
                sub.Handler = null;
                sub.Id = 0;
                Pool.Push(sub);
            }
        }

        #endregion

        #region 静态字段

        // 类型事件通道：Type -> EventChannel
        private static readonly Dictionary<Type, EventChannel> sTypeChannels = new(16);

        // 枚举键事件通道：(EnumType, EnumValue) -> EventChannel
        private static readonly Dictionary<(Type, int), EventChannel> sEnumChannels = new(32);

        // 订阅 ID 到通道的映射（用于快速取消订阅）
        private static readonly Dictionary<int, (EventChannel Channel, Subscription Sub)> sSubscriptionMap = new(64);

        // 全局订阅 ID 计数器
        private static int sNextGlobalId = 1;

        // 临时列表（避免遍历时修改集合）
        private static readonly List<Subscription> sTempList = new(16);

        #endregion

        #region 类型事件 API

        /// <summary>
        /// 注册类型事件（无 Owner）
        /// </summary>
        /// <typeparam name="T">事件数据类型</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public static IDisposable Register<T>(Action<T> handler)
        {
            return RegisterInternal<T>(null, handler);
        }

        /// <summary>
        /// 注册类型事件（带 Owner，用于批量清理）
        /// </summary>
        /// <typeparam name="T">事件数据类型</typeparam>
        /// <param name="owner">订阅者对象（通常是 EditorWindow 或 ToolPage）</param>
        /// <param name="handler">事件处理器</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public static IDisposable Register<T>(object owner, Action<T> handler)
        {
            return RegisterInternal<T>(owner, handler);
        }

        /// <summary>
        /// 发送类型事件
        /// </summary>
        /// <typeparam name="T">事件数据类型</typeparam>
        /// <param name="args">事件数据</param>
        public static void Send<T>(T args)
        {
            var type = typeof(T);
            if (!sTypeChannels.TryGetValue(type, out var channel)) return;
            if (channel.Subscriptions.Count == 0) return;

            // 复制到临时列表，避免遍历时修改
            sTempList.Clear();
            for (int i = 0; i < channel.Subscriptions.Count; i++)
            {
                sTempList.Add(channel.Subscriptions[i]);
            }

            // 遍历执行
            for (int i = 0; i < sTempList.Count; i++)
            {
                var sub = sTempList[i];
                if (sub.Handler is Action<T> callback)
                {
                    try
                    {
                        callback(args);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorEventCenter] 类型事件 '{type.Name}' 处理器异常: {ex}");
                    }
                }
            }

            sTempList.Clear();
        }

        #endregion

        #region 枚举键事件 API

        /// <summary>
        /// 注册枚举键事件（无 Owner）
        /// </summary>
        /// <typeparam name="TKey">枚举键类型</typeparam>
        /// <typeparam name="TValue">事件数据类型</typeparam>
        /// <param name="key">枚举键</param>
        /// <param name="handler">事件处理器</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public static IDisposable Register<TKey, TValue>(TKey key, Action<TValue> handler) where TKey : Enum
        {
            return RegisterEnumInternal<TKey, TValue>(null, key, handler);
        }

        /// <summary>
        /// 注册枚举键事件（带 Owner）
        /// </summary>
        public static IDisposable Register<TKey, TValue>(object owner, TKey key, Action<TValue> handler) where TKey : Enum
        {
            return RegisterEnumInternal<TKey, TValue>(owner, key, handler);
        }

        /// <summary>
        /// 发送枚举键事件
        /// </summary>
        /// <typeparam name="TKey">枚举键类型</typeparam>
        /// <typeparam name="TValue">事件数据类型</typeparam>
        /// <param name="key">枚举键</param>
        /// <param name="args">事件数据</param>
        public static void Send<TKey, TValue>(TKey key, TValue args) where TKey : Enum
        {
            var channelKey = (typeof(TKey), Convert.ToInt32(key));
            if (!sEnumChannels.TryGetValue(channelKey, out var channel)) return;
            if (channel.Subscriptions.Count == 0) return;

            // 复制到临时列表
            sTempList.Clear();
            for (int i = 0; i < channel.Subscriptions.Count; i++)
            {
                sTempList.Add(channel.Subscriptions[i]);
            }

            // 遍历执行
            for (int i = 0; i < sTempList.Count; i++)
            {
                var sub = sTempList[i];
                if (sub.Handler is Action<TValue> callback)
                {
                    try
                    {
                        callback(args);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorEventCenter] 枚举事件 '{typeof(TKey).Name}.{key}' 处理器异常: {ex}");
                    }
                }
            }

            sTempList.Clear();
        }

        #endregion

        #region 批量清理 API

        /// <summary>
        /// 取消指定 Owner 的所有订阅
        /// 通常在 EditorWindow.OnDisable 或 ToolPage.OnDeactivate 中调用
        /// </summary>
        /// <param name="owner">订阅者对象</param>
        public static void UnregisterAll(object owner)
        {
            if (owner is null) return;

            // 收集要移除的订阅 ID
            var toRemove = ListPool<int>.Get();
            try
            {
                foreach (var kvp in sSubscriptionMap)
                {
                    if (kvp.Value.Sub.Owner == owner)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                // 移除订阅
                for (int i = 0; i < toRemove.Count; i++)
                {
                    UnregisterById(toRemove[i]);
                }
            }
            finally
            {
                ListPool<int>.Return(toRemove);
            }
        }

        /// <summary>
        /// 清理所有订阅（PlayMode 退出时自动调用）
        /// </summary>
        public static void ClearAll()
        {
            sTypeChannels.Clear();
            sEnumChannels.Clear();
            sSubscriptionMap.Clear();
        }

        #endregion

        #region 内部实现

        private static IDisposable RegisterInternal<T>(object owner, Action<T> handler)
        {
            if (handler is null) return Disposable.Empty;

            var type = typeof(T);
            if (!sTypeChannels.TryGetValue(type, out var channel))
            {
                channel = new();
                sTypeChannels[type] = channel;
            }

            var sub = channel.GetSubscription();
            sub.Owner = owner;
            sub.Handler = handler;
            sub.Id = sNextGlobalId++;

            channel.Subscriptions.Add(sub);
            sSubscriptionMap[sub.Id] = (channel, sub);

            var subId = sub.Id;
            return Disposable.Create(() => UnregisterById(subId));
        }

        private static IDisposable RegisterEnumInternal<TKey, TValue>(object owner, TKey key, Action<TValue> handler) where TKey : Enum
        {
            if (handler is null) return Disposable.Empty;

            var channelKey = (typeof(TKey), Convert.ToInt32(key));
            if (!sEnumChannels.TryGetValue(channelKey, out var channel))
            {
                channel = new();
                sEnumChannels[channelKey] = channel;
            }

            var sub = channel.GetSubscription();
            sub.Owner = owner;
            sub.Handler = handler;
            sub.Id = sNextGlobalId++;

            channel.Subscriptions.Add(sub);
            sSubscriptionMap[sub.Id] = (channel, sub);

            var subId = sub.Id;
            return Disposable.Create(() => UnregisterById(subId));
        }

        private static void UnregisterById(int id)
        {
            if (!sSubscriptionMap.TryGetValue(id, out var entry)) return;

            sSubscriptionMap.Remove(id);
            entry.Channel.Subscriptions.Remove(entry.Sub);
            entry.Channel.ReturnSubscription(entry.Sub);
        }

        #endregion

        #region 初始化

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // PlayMode 退出时清理所有订阅
            EditorApplication.playModeStateChanged += static state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    ClearAll();
                }
            };
        }

        #endregion

        #region 调试 API

        /// <summary>
        /// 获取类型事件订阅数量（调试用）
        /// </summary>
        public static int GetSubscriberCount<T>()
        {
            return sTypeChannels.TryGetValue(typeof(T), out var channel) ? channel.Subscriptions.Count : 0;
        }

        /// <summary>
        /// 获取枚举键事件订阅数量（调试用）
        /// </summary>
        public static int GetSubscriberCount<TKey>(TKey key) where TKey : Enum
        {
            var channelKey = (typeof(TKey), Convert.ToInt32(key));
            return sEnumChannels.TryGetValue(channelKey, out var channel) ? channel.Subscriptions.Count : 0;
        }

        #endregion
    }

    /// <summary>
    /// 编辑器事件类型枚举
    /// </summary>
    public enum EditorEventType
    {
        // PoolKit 事件
        PoolListChanged,
        PoolActiveChanged,
        PoolEventLogged,

        // EventKit 事件
        EventTriggered,
        EventRegistered,
        EventUnregistered,

        // FsmKit 事件
        FsmStateChanged,
        FsmRegistered,

        // ResKit 事件
        ResLoaded,
        ResUnloaded
    }

}
#endif
