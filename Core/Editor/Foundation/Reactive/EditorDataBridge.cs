#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Shared editor-only data bus used by runtime debug hooks and editor pages.
    /// </summary>
    /// <remarks>
    /// This bridge is compiled only in the Unity Editor. Publish methods are also guarded by
    /// <see cref="ConditionalAttribute"/>, so player builds do not execute these notification calls.
    /// The goal is to keep runtime/editor collaboration decoupled while avoiding editor overhead in builds.
    /// </remarks>
    public static class EditorDataBridge
    {
        /// <summary>
        /// Maps a logical channel name to its registered listeners.
        /// </summary>
        private static readonly Dictionary<string, List<Delegate>> sChannels = new(16);

        /// <summary>
        /// Stores the last trigger time for throttled subscriptions.
        /// </summary>
        /// <remarks>
        /// The key format is <c>channel + callback hash</c>.
        /// </remarks>
        private static readonly Dictionary<string, double> sThrottleTimestamps = new(8);

        #region Publish

        /// <summary>
        /// Publishes a payload message to the specified channel.
        /// </summary>
        /// <typeparam name="T">Payload type.</typeparam>
        /// <param name="channel">Logical channel name. Prefer centralized constants.</param>
        /// <param name="data">Payload for the current notification.</param>
        [Conditional("UNITY_EDITOR")]
        public static void NotifyDataChanged<T>(string channel, T data)
        {
            if (!sChannels.TryGetValue(channel, out var listeners)) return;
            if (listeners.Count == 0) return;

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i] is Action<T> callback)
                {
                    try
                    {
                        callback(data);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Publishes a notification without an explicit payload.
        /// </summary>
        /// <param name="channel">Logical channel name.</param>
        [Conditional("UNITY_EDITOR")]
        public static void NotifyDataChanged(string channel)
        {
            NotifyDataChanged<object>(channel, null);
        }

        #endregion

        #region Subscribe

        /// <summary>
        /// Subscribes to a channel and receives strongly typed payloads.
        /// </summary>
        /// <typeparam name="T">Expected payload type.</typeparam>
        /// <param name="channel">Logical channel name.</param>
        /// <param name="callback">Callback invoked when the channel publishes data.</param>
        /// <returns>A disposable handle used to unregister the subscription.</returns>
        public static IDisposable Subscribe<T>(string channel, Action<T> callback)
        {
            if (string.IsNullOrEmpty(channel) || callback is null)
                return Disposable.Empty;

            if (!sChannels.TryGetValue(channel, out var listeners))
            {
                listeners = new List<Delegate>(4);
                sChannels[channel] = listeners;
            }

            listeners.Add(callback);

            return Disposable.Create(() =>
            {
                listeners.Remove(callback);
                if (listeners.Count == 0)
                {
                    sChannels.Remove(channel);
                }
            });
        }

        /// <summary>
        /// Subscribes to a channel with a minimum trigger interval.
        /// </summary>
        /// <typeparam name="T">Expected payload type.</typeparam>
        /// <param name="channel">Logical channel name.</param>
        /// <param name="callback">Callback invoked after throttling passes.</param>
        /// <param name="intervalSeconds">Minimum interval in seconds between callbacks.</param>
        /// <returns>A disposable handle used to unregister the subscription.</returns>
        /// <remarks>
        /// The current implementation uses leading-edge throttling.
        /// Messages that arrive within the interval are skipped rather than replayed later.
        /// </remarks>
        public static IDisposable SubscribeThrottled<T>(
            string channel,
            Action<T> callback,
            float intervalSeconds)
        {
            if (string.IsNullOrEmpty(channel) || callback is null)
                return Disposable.Empty;

            string throttleKey = $"{channel}_throttle_{callback.GetHashCode()}";

            return Subscribe<T>(channel, data =>
            {
                var now = EditorApplication.timeSinceStartup;

                if (sThrottleTimestamps.TryGetValue(throttleKey, out var lastTime) &&
                    now - lastTime < intervalSeconds)
                {
                    return;
                }

                sThrottleTimestamps[throttleKey] = now;
                callback(data);
            });
        }

        #endregion

        #region Maintenance

        /// <summary>
        /// Clears all subscriptions and throttle state.
        /// </summary>
        /// <remarks>
        /// Typically called when leaving Play Mode.
        /// </remarks>
        public static void ClearAll()
        {
            sChannels.Clear();
            sThrottleTimestamps.Clear();
        }

        /// <summary>
        /// Clears every subscription registered on a specific channel.
        /// </summary>
        /// <param name="channel">Logical channel name.</param>
        public static void ClearChannel(string channel)
        {
            sChannels.Remove(channel);
        }

        /// <summary>
        /// Gets the current subscriber count for a channel.
        /// </summary>
        /// <param name="channel">Logical channel name.</param>
        /// <returns>The number of active subscribers on the channel.</returns>
        public static int GetSubscriberCount(string channel)
        {
            return sChannels.TryGetValue(channel, out var listeners) ? listeners.Count : 0;
        }

        #endregion

        #region Initialize

        /// <summary>
        /// Hooks editor lifecycle cleanup.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    ClearAll();
                }
            };
        }

        #endregion
    }

    /// <summary>
    /// Base lifecycle template for editor bridges that track Play Mode transitions.
    /// </summary>
    /// <remarks>
    /// Derived bridges can use this class to react to Unity editor state changes and schedule delayed
    /// initialization when runtime systems are not ready immediately after entering Play Mode.
    /// </remarks>
    public abstract class PlayModeEditorBridgeBase
    {
        /// <summary>
        /// Initializes the bridge and registers Play Mode lifecycle hooks.
        /// </summary>
        protected PlayModeEditorBridgeBase()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedInternal;
            OnCreated();
        }

        /// <summary>
        /// Called after the bridge instance is created.
        /// </summary>
        protected virtual void OnCreated() { }

        /// <summary>
        /// Called after entering Play Mode.
        /// </summary>
        protected virtual void OnEnteredPlayMode() { }

        /// <summary>
        /// Called before exiting Play Mode.
        /// </summary>
        protected virtual void OnExitingPlayMode() { }

        /// <summary>
        /// Called after returning to Edit Mode.
        /// </summary>
        protected virtual void OnEnteredEditMode() { }

        /// <summary>
        /// Called before leaving Edit Mode.
        /// </summary>
        protected virtual void OnExitingEditMode() { }

        /// <summary>
        /// Schedules an action through one or more <see cref="EditorApplication.delayCall"/> passes.
        /// </summary>
        /// <param name="action">Final action to execute.</param>
        /// <param name="delayCallCount">Number of chained <c>delayCall</c> passes.</param>
        protected static void ScheduleDelayedAction(Action action, int delayCallCount = 1)
        {
            if (action == null) return;

            if (delayCallCount <= 1)
            {
                EditorApplication.delayCall += () => action();
                return;
            }

            void ScheduleNext()
            {
                delayCallCount--;
                if (delayCallCount <= 1)
                {
                    EditorApplication.delayCall += () => action();
                    return;
                }

                EditorApplication.delayCall += ScheduleNext;
            }

            EditorApplication.delayCall += ScheduleNext;
        }

        /// <summary>
        /// Routes Unity Play Mode events to the derived lifecycle callbacks.
        /// </summary>
        /// <param name="state">Current Play Mode state.</param>
        private void OnPlayModeStateChangedInternal(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    OnEnteredPlayMode();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    OnExitingPlayMode();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    OnEnteredEditMode();
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    OnExitingEditMode();
                    break;
            }
        }
    }

    /// <summary>
    /// Base class for bridges that hook <see cref="EasyEventEditorHook"/> and forward selected events.
    /// </summary>
    /// <remarks>
    /// This is intended for runtime debug streams that already emit through EasyEvent and only need an
    /// editor-side adapter layer to publish data into <see cref="EditorDataBridge"/>.
    /// </remarks>
    public abstract class EasyEventSendHookBridgeBase : PlayModeEditorBridgeBase
    {
        private Action<string, string, object> mOriginalOnSend;
        private bool mIsHookInstalled;

        /// <summary>
        /// Schedules hook installation after the bridge is created.
        /// </summary>
        protected override void OnCreated()
        {
            ScheduleHookInstall();
        }

        /// <summary>
        /// Reinstalls the hook after entering Play Mode again.
        /// </summary>
        protected override void OnEnteredPlayMode()
        {
            mIsHookInstalled = false;
            ScheduleHookInstall();
        }

        /// <summary>
        /// Number of delayed editor frames used before installing the hook.
        /// </summary>
        /// <remarks>
        /// Some runtime systems finish initialization after Play Mode starts. Derived bridges can increase
        /// this value when their source systems are not ready immediately.
        /// </remarks>
        protected virtual int HookInstallDelayCallCount => 2;

        /// <summary>
        /// Determines whether the current runtime event should be handled by the bridge.
        /// </summary>
        /// <param name="eventType">Event category.</param>
        /// <param name="eventKey">Event key.</param>
        /// <param name="args">Event payload.</param>
        /// <returns><see langword="true"/> when the event should be forwarded.</returns>
        protected virtual bool ShouldHandleEvent(string eventType, string eventKey, object args)
        {
            return eventType == "Type";
        }

        /// <summary>
        /// Handles a runtime event selected by <see cref="ShouldHandleEvent"/>.
        /// </summary>
        /// <param name="eventType">Event category.</param>
        /// <param name="eventKey">Event key.</param>
        /// <param name="args">Event payload.</param>
        protected abstract void HandleEvent(string eventType, string eventKey, object args);

        /// <summary>
        /// Schedules delayed hook installation.
        /// </summary>
        protected void ScheduleHookInstall()
        {
            ScheduleDelayedAction(InstallHook, HookInstallDelayCallCount);
        }

        /// <summary>
        /// Installs the wrapped send hook and preserves the original callback chain.
        /// </summary>
        private void InstallHook()
        {
            if (mIsHookInstalled) return;
            mIsHookInstalled = true;

            mOriginalOnSend = EasyEventEditorHook.OnSend;
            EasyEventEditorHook.OnSend = OnSendWrapped;
        }

        /// <summary>
        /// Invokes the original hook first, then forwards eligible events to the derived bridge.
        /// </summary>
        private void OnSendWrapped(string eventType, string eventKey, object args)
        {
            mOriginalOnSend?.Invoke(eventType, eventKey, args);

            if (!ShouldHandleEvent(eventType, eventKey, args)) return;
            HandleEvent(eventType, eventKey, args);
        }
    }
}
#endif
