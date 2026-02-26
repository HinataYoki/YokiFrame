using System;
using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 对象池调试器 - 运行时数据收集
    /// 仅在编辑器模式下启用追踪功能
    /// </summary>
    public static partial class PoolDebugger
    {
#if UNITY_EDITOR
        #region 事件通道常量

        /// <summary>
        /// 池列表变化事件通道
        /// </summary>
        public const string CHANNEL_POOL_LIST_CHANGED = "PoolKit.PoolListChanged";

        /// <summary>
        /// 活跃对象变化事件通道
        /// </summary>
        public const string CHANNEL_POOL_ACTIVE_CHANGED = "PoolKit.PoolActiveChanged";

        /// <summary>
        /// 事件日志事件通道
        /// </summary>
        public const string CHANNEL_POOL_EVENT_LOGGED = "PoolKit.PoolEventLogged";

        #endregion

        #region 反射缓存（避免重复查找）

        private static System.Reflection.MethodInfo sCachedNotifyMethodDefinition;
        private static readonly Dictionary<Type, System.Reflection.MethodInfo> sCachedGenericMethods = new();
        private static bool sReflectionInitialized;
        
        // 参数数组缓存（避免每次分配）
        private static readonly object[] sInvokeParams = new object[2];

        /// <summary>
        /// 初始化反射缓存（仅执行一次）
        /// </summary>
        private static void InitializeReflectionCache()
        {
            if (sReflectionInitialized) return;
            sReflectionInitialized = true;

            var bridgeType = Type.GetType("YokiFrame.EditorTools.EditorDataBridge, YokiFrame.Core.Editor");
            if (bridgeType is null) return;

            var methods = bridgeType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name != "Notify DataChanged" || !m.IsGenericMethodDefinition) continue;
                var parameters = m.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string))
                {
                    sCachedNotifyMethodDefinition = m;
                    break;
                }
            }
        }

        /// <summary>
        /// 获取缓存的泛型方法实例
        /// </summary>
        private static System.Reflection.MethodInfo GetCachedGenericMethod(Type dataType)
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

        #endregion

        /// <summary>
        /// 通知编辑器数据变化（通过反射调用 EditorDataBridge）
        /// 避免运行时程序集直接引用编辑器程序集
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void NotifyEditorDataChanged<T>(string channel, T data)
        {
            if (!EnableTracking) return;

            InitializeReflectionCache();
            var genericMethod = GetCachedGenericMethod(typeof(T));
            if (genericMethod is null) return;

            try
            {
                // 复用参数数组，避免每次分配
                sInvokeParams[0] = channel;
                sInvokeParams[1] = data;
                genericMethod.Invoke(null, sInvokeParams);
                // 清空引用，避免持有对象
                sInvokeParams[0] = null;
                sInvokeParams[1] = null;
            }
            catch
            {
                // 静默失败，避免影响运行时性能
            }
        }

        /// <summary>
        /// 事件历史最大记录数
        /// </summary>
        public const int MAX_EVENT_HISTORY = 200;

        /// <summary>
        /// 池调试信息字典
        /// </summary>
        private static readonly Dictionary<object, PoolDebugInfo> sPools = new();

        /// <summary>
        /// 对象到池的映射（用于快速查找）
        /// </summary>
        private static readonly Dictionary<object, object> sObjectToPool = new();

        /// <summary>
        /// 事件历史队列
        /// </summary>
        private static readonly Queue<PoolEvent> sEventHistory = new(MAX_EVENT_HISTORY);

        /// <summary>
        /// 是否启用追踪
        /// </summary>
        public static bool EnableTracking { get; set; } = true;

        /// <summary>
        /// 是否记录堆栈（性能开销较大）
        /// </summary>
        public static bool EnableStackTrace { get; set; } = true;

        /// <summary>
        /// 是否记录事件历史
        /// </summary>
        public static bool EnableEventHistory { get; set; } = true;

        /// <summary>
        /// 获取池数量
        /// </summary>
        public static int PoolCount => sPools.Count;

        /// <summary>
        /// 事件历史数量
        /// </summary>
        public static int EventHistoryCount => sEventHistory.Count;

        /// <summary>
        /// 清空所有追踪数据
        /// </summary>
        public static void Clear()
        {
            sPools.Clear();
            sObjectToPool.Clear();
            sEventHistory.Clear();
        }
#else
        // 非编辑器模式下的空实现
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
#endif
    }
}
