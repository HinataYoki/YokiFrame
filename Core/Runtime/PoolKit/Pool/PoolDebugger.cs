using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// Shared reflection publisher used by runtime-safe monitor code to push data into the Editor bus
    /// without directly referencing the Editor assembly.
    /// </summary>
    public static class EditorBridgeReflectionUtility
    {
#if UNITY_EDITOR
        private static System.Reflection.MethodInfo sCachedNotifyMethodDefinition;
        private static readonly Dictionary<System.Type, System.Reflection.MethodInfo> sCachedGenericMethods = new();
        private static bool sReflectionInitialized;
        private static readonly object[] sInvokeParams = new object[2];
#endif

        /// <summary>
        /// Publishes a payload to <c>EditorDataBridge</c> through reflection.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void NotifyDataChanged<T>(string channel, T data)
        {
#if UNITY_EDITOR
            InitializeReflectionCache();
            var genericMethod = GetCachedGenericMethod(typeof(T));
            if (genericMethod is null) return;

            try
            {
                sInvokeParams[0] = channel;
                sInvokeParams[1] = data;
                genericMethod.Invoke(null, sInvokeParams);
            }
            finally
            {
                sInvokeParams[0] = null;
                sInvokeParams[1] = null;
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Initializes the reflection cache for <c>EditorDataBridge.NotifyDataChanged&lt;T&gt;</c>.
        /// </summary>
        private static void InitializeReflectionCache()
        {
            if (sReflectionInitialized) return;
            sReflectionInitialized = true;

            var bridgeType = System.Type.GetType("YokiFrame.EditorTools.EditorDataBridge, YokiFrame.Core.Editor");
            if (bridgeType is null) return;

            var methods = bridgeType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != "NotifyDataChanged" || !method.IsGenericMethodDefinition) continue;

                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string))
                {
                    sCachedNotifyMethodDefinition = method;
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the cached generic publish method for the specified payload type.
        /// </summary>
        private static System.Reflection.MethodInfo GetCachedGenericMethod(System.Type dataType)
        {
            if (sCachedGenericMethods.TryGetValue(dataType, out var method))
            {
                return method;
            }

            if (sCachedNotifyMethodDefinition is null) return null;

            var genericMethod = sCachedNotifyMethodDefinition.MakeGenericMethod(dataType);
            sCachedGenericMethods[dataType] = genericMethod;
            return genericMethod;
        }
#endif
    }

    /// <summary>
    /// PoolKit runtime monitor publisher.
    /// Collects object-pool diagnostics and forwards relevant changes to the shared Editor bus.
    /// </summary>
    public static partial class PoolDebugger
    {
#if UNITY_EDITOR
        #region Channels

        /// <summary>
        /// Published when the pool list changes.
        /// Payload type: <see cref="PoolDebugInfo"/>.
        /// </summary>
        public const string CHANNEL_POOL_LIST_CHANGED = "PoolKit.PoolListChanged";

        /// <summary>
        /// Published when active objects in a pool change.
        /// Payload type: <see cref="PoolDebugInfo"/>.
        /// </summary>
        public const string CHANNEL_POOL_ACTIVE_CHANGED = "PoolKit.PoolActiveChanged";

        /// <summary>
        /// Published when a pool event log entry is appended.
        /// Payload type: <see cref="PoolEvent"/>.
        /// </summary>
        public const string CHANNEL_POOL_EVENT_LOGGED = "PoolKit.PoolEventLogged";

        #endregion

        /// <summary>
        /// Forwards a pool-monitor message to the Editor bus.
        /// Publishing is skipped when tracking is disabled.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void NotifyEditorDataChanged<T>(string channel, T data)
        {
            if (!EnableTracking) return;
            EditorBridgeReflectionUtility.NotifyDataChanged(channel, data);
        }

        #region Runtime Snapshot State

        /// <summary>
        /// Maximum number of event-history entries retained in memory.
        /// </summary>
        public const int MAX_EVENT_HISTORY = 200;

        /// <summary>
        /// Map from pool instance to runtime debug info.
        /// </summary>
        private static readonly Dictionary<object, PoolDebugInfo> sPools = new();

        /// <summary>
        /// Map from active object to owning pool, used for quick tracked-object checks.
        /// </summary>
        private static readonly Dictionary<object, object> sObjectToPool = new();

        /// <summary>
        /// Queue of recent pool events.
        /// </summary>
        private static readonly Queue<PoolEvent> sEventHistory = new(MAX_EVENT_HISTORY);

        /// <summary>
        /// Whether pool tracking is enabled.
        /// </summary>
        public static bool EnableTracking { get; set; } = true;

        /// <summary>
        /// Whether stack traces should be recorded for tracked actions.
        /// This improves diagnostics but increases runtime cost.
        /// </summary>
        public static bool EnableStackTrace { get; set; } = false;

        /// <summary>
        /// Whether pool events should be recorded into history.
        /// </summary>
        public static bool EnableEventHistory { get; set; } = true;

        /// <summary>
        /// Number of currently tracked pools.
        /// </summary>
        public static int PoolCount => sPools.Count;

        /// <summary>
        /// Number of retained event-history entries.
        /// </summary>
        public static int EventHistoryCount => sEventHistory.Count;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Clears all runtime pool-monitor caches.
        /// This method remains the legacy public reset entry for existing callers.
        /// </summary>
        public static void Clear()
        {
            sPools.Clear();
            sObjectToPool.Clear();
            sEventHistory.Clear();
        }

        /// <summary>
        /// Clears all runtime state retained by the pool monitor publisher.
        /// This is the unified lifecycle reset entry used by the legacy monitor contract.
        /// </summary>
        public static void ClearRuntimeMonitorState() => Clear();

        #endregion
#else
        public const int MAX_EVENT_HISTORY = 200;
        public const string CHANNEL_POOL_LIST_CHANGED = "PoolKit.PoolListChanged";
        public const string CHANNEL_POOL_ACTIVE_CHANGED = "PoolKit.PoolActiveChanged";
        public const string CHANNEL_POOL_EVENT_LOGGED = "PoolKit.PoolEventLogged";

        public static bool EnableTracking { get; set; }
        public static bool EnableStackTrace { get; set; }
        public static bool EnableEventHistory { get; set; }
        public static int PoolCount => 0;
        public static int EventHistoryCount => 0;

        public static void RegisterPool(object pool, string name) { }
        public static void RegisterPool(object pool, string name, int maxCacheCount) { }
        public static void UnregisterPool(object pool) { }
        public static void TrackAllocate(object pool, object obj) { }
        public static void TrackRecycle(object pool, object obj) { }
        public static void UpdateTotalCount(object pool, int totalCount) { }
        public static int GetActiveCount(object pool) => 0;
        public static void UpdateMaxCacheCount(object pool, int maxCacheCount) { }
        public static void GetAllPools(List<PoolDebugInfo> result) => result.Clear();
        public static bool ForceReturn(object pool, object obj) => false;
        public static void ClearEventHistory() { }
        public static void Clear() { }
        public static void ClearRuntimeMonitorState() { }
#endif
    }
}
