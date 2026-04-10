#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Editor-only event hub for editor windows, tool pages, and editor services.
    /// </summary>
    /// <remarks>
    /// This event center is intentionally independent from runtime EventKit so editor communication can evolve
    /// without introducing player-build dependencies. It supports strongly typed events, enum-keyed events,
    /// owner-based cleanup, and low-allocation subscription reuse.
    /// </remarks>
    public static class EditorEventCenter
    {
        #region Internal Types

        /// <summary>
        /// Single subscription record stored inside an event channel.
        /// </summary>
        private sealed class Subscription
        {
            public object Owner;
            public Delegate Handler;
            public int Id;
        }

        /// <summary>
        /// Per-channel container that owns active subscriptions and a small reusable pool.
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

        #region Storage

        /// <summary>
        /// Typed event channels keyed by payload type.
        /// </summary>
        private static readonly Dictionary<Type, EventChannel> sTypeChannels = new(16);

        /// <summary>
        /// Enum-keyed event channels keyed by <c>(enum type, enum value)</c>.
        /// </summary>
        private static readonly Dictionary<(Type, int), EventChannel> sEnumChannels = new(32);

        /// <summary>
        /// Maps subscription id to its owning channel for fast unregistration.
        /// </summary>
        private static readonly Dictionary<int, (EventChannel Channel, Subscription Sub)> sSubscriptionMap = new(64);

        /// <summary>
        /// Global incrementing id source for subscriptions.
        /// </summary>
        private static int sNextGlobalId = 1;

        /// <summary>
        /// Temporary list used to avoid mutating subscription collections while iterating.
        /// </summary>
        private static readonly List<Subscription> sTempList = new(16);

        #endregion

        #region Typed Events

        /// <summary>
        /// Registers a typed event handler without an explicit owner.
        /// </summary>
        /// <typeparam name="T">Event payload type.</typeparam>
        /// <param name="handler">Handler invoked when the event is sent.</param>
        /// <returns>A disposable used to unregister the handler.</returns>
        public static IDisposable Register<T>(Action<T> handler)
        {
            return RegisterInternal(null, handler);
        }

        /// <summary>
        /// Registers a typed event handler with an owner for batch cleanup.
        /// </summary>
        /// <typeparam name="T">Event payload type.</typeparam>
        /// <param name="owner">Subscription owner, usually an editor window or tool page.</param>
        /// <param name="handler">Handler invoked when the event is sent.</param>
        /// <returns>A disposable used to unregister the handler.</returns>
        public static IDisposable Register<T>(object owner, Action<T> handler)
        {
            return RegisterInternal(owner, handler);
        }

        /// <summary>
        /// Sends a typed editor event.
        /// </summary>
        /// <typeparam name="T">Event payload type.</typeparam>
        /// <param name="args">Event payload.</param>
        public static void Send<T>(T args)
        {
            var type = typeof(T);
            if (!sTypeChannels.TryGetValue(type, out var channel)) return;
            if (channel.Subscriptions.Count == 0) return;

            sTempList.Clear();
            for (int i = 0; i < channel.Subscriptions.Count; i++)
            {
                sTempList.Add(channel.Subscriptions[i]);
            }

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
                        Debug.LogError($"[EditorEventCenter] Typed event '{type.Name}' handler failed: {ex}");
                    }
                }
            }

            sTempList.Clear();
        }

        #endregion

        #region Enum-Keyed Events

        /// <summary>
        /// Registers an enum-keyed event handler without an explicit owner.
        /// </summary>
        /// <typeparam name="TKey">Enum type used as the channel key.</typeparam>
        /// <typeparam name="TValue">Event payload type.</typeparam>
        /// <param name="key">Enum key.</param>
        /// <param name="handler">Handler invoked when the matching event is sent.</param>
        /// <returns>A disposable used to unregister the handler.</returns>
        public static IDisposable Register<TKey, TValue>(TKey key, Action<TValue> handler) where TKey : Enum
        {
            return RegisterEnumInternal(null, key, handler);
        }

        /// <summary>
        /// Registers an enum-keyed event handler with an owner for batch cleanup.
        /// </summary>
        /// <typeparam name="TKey">Enum type used as the channel key.</typeparam>
        /// <typeparam name="TValue">Event payload type.</typeparam>
        /// <param name="owner">Subscription owner, usually an editor window or tool page.</param>
        /// <param name="key">Enum key.</param>
        /// <param name="handler">Handler invoked when the matching event is sent.</param>
        /// <returns>A disposable used to unregister the handler.</returns>
        public static IDisposable Register<TKey, TValue>(object owner, TKey key, Action<TValue> handler) where TKey : Enum
        {
            return RegisterEnumInternal(owner, key, handler);
        }

        /// <summary>
        /// Sends an enum-keyed editor event.
        /// </summary>
        /// <typeparam name="TKey">Enum type used as the channel key.</typeparam>
        /// <typeparam name="TValue">Event payload type.</typeparam>
        /// <param name="key">Enum key.</param>
        /// <param name="args">Event payload.</param>
        public static void Send<TKey, TValue>(TKey key, TValue args) where TKey : Enum
        {
            var channelKey = (typeof(TKey), Convert.ToInt32(key));
            if (!sEnumChannels.TryGetValue(channelKey, out var channel)) return;
            if (channel.Subscriptions.Count == 0) return;

            sTempList.Clear();
            for (int i = 0; i < channel.Subscriptions.Count; i++)
            {
                sTempList.Add(channel.Subscriptions[i]);
            }

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
                        Debug.LogError($"[EditorEventCenter] Enum event '{typeof(TKey).Name}.{key}' handler failed: {ex}");
                    }
                }
            }

            sTempList.Clear();
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Unregisters every subscription owned by the specified object.
        /// </summary>
        /// <param name="owner">Subscription owner.</param>
        /// <remarks>
        /// Typically called from <c>EditorWindow.OnDisable</c> or <c>IYokiToolPage.OnDeactivate</c>.
        /// </remarks>
        public static void UnregisterAll(object owner)
        {
            if (owner is null) return;

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
        /// Clears all editor event subscriptions.
        /// </summary>
        /// <remarks>
        /// This is automatically called when exiting Play Mode.
        /// </remarks>
        public static void ClearAll()
        {
            sTypeChannels.Clear();
            sEnumChannels.Clear();
            sSubscriptionMap.Clear();
        }

        #endregion

        #region Internal Implementation

        private static IDisposable RegisterInternal<T>(object owner, Action<T> handler)
        {
            if (handler is null) return Disposable.Empty;

            var type = typeof(T);
            if (!sTypeChannels.TryGetValue(type, out var channel))
            {
                channel = new EventChannel();
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
                channel = new EventChannel();
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

        #region Initialization

        /// <summary>
        /// Hooks Play Mode cleanup.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged += static state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    ClearAll();
                }
            };
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Gets the current subscriber count for a typed event.
        /// </summary>
        public static int GetSubscriberCount<T>()
        {
            return sTypeChannels.TryGetValue(typeof(T), out var channel) ? channel.Subscriptions.Count : 0;
        }

        /// <summary>
        /// Gets the current subscriber count for a specific enum-keyed event.
        /// </summary>
        public static int GetSubscriberCount<TKey>(TKey key) where TKey : Enum
        {
            var channelKey = (typeof(TKey), Convert.ToInt32(key));
            return sEnumChannels.TryGetValue(channelKey, out var channel) ? channel.Subscriptions.Count : 0;
        }

        #endregion
    }

    /// <summary>
    /// Legacy editor event keys used by older monitor implementations.
    /// </summary>
    /// <remarks>
    /// New shared editor communication should prefer explicit payload types or
    /// <see cref="EditorChannelRegistry"/>-based metadata contracts. This enum remains for
    /// compatibility with existing monitors during the migration period.
    /// </remarks>
    public enum EditorEventType
    {
        PoolListChanged,
        PoolActiveChanged,
        PoolEventLogged,

        EventTriggered,
        EventRegistered,
        EventUnregistered,

        FsmStateChanged,
        FsmRegistered,

        ResLoaded,
        ResUnloaded
    }
}
#endif
