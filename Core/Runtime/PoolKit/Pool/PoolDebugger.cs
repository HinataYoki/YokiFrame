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
    public static class PoolDebugger
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
        
        /// <summary>
        /// 通知编辑器数据变化（通过反射调用 EditorDataBridge）
        /// 避免运行时程序集直接引用编辑器程序集
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void NotifyEditorDataChanged<T>(string channel, T data)
        {
            var bridgeType = Type.GetType("YokiFrame.EditorTools.EditorDataBridge, YokiFrame.Core.Editor");
            if (bridgeType == null) return;
            
            // 获取泛型方法：NotifyDataChanged<T>(string, T)
            // 使用 GetMethods 遍历避免 AmbiguousMatchException
            var methods = bridgeType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            System.Reflection.MethodInfo targetMethod = null;
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name != "NotifyDataChanged" || !m.IsGenericMethodDefinition) continue;
                var parameters = m.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string))
                {
                    targetMethod = m;
                    break;
                }
            }
            
            if (targetMethod == null) return;
            
            var genericMethod = targetMethod.MakeGenericMethod(typeof(T));
            genericMethod.Invoke(null, new object[] { channel, data });
        }
        
        /// <summary>
        /// 泄露检测阈值（秒）
        /// </summary>
        public const float LEAK_THRESHOLD_SECONDS = 30f;
        
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
        /// 注册池到调试器
        /// </summary>
        public static void RegisterPool(object pool, string name)
        {
            if (pool == default || !EnableTracking) return;
            
            if (sPools.ContainsKey(pool)) return;
            
            var info = new PoolDebugInfo
            {
                Name = name,
                TypeName = pool.GetType().Name,
                PoolRef = pool
            };
            sPools[pool] = info;
            
            NotifyEditorDataChanged(CHANNEL_POOL_LIST_CHANGED, info);
        }

        /// <summary>
        /// 注销池
        /// </summary>
        public static void UnregisterPool(object pool)
        {
            if (pool == default) return;
            
            if (sPools.TryGetValue(pool, out var info))
            {
                foreach (var activeObj in info.ActiveObjects)
                {
                    if (activeObj.Obj != default)
                    {
                        sObjectToPool.Remove(activeObj.Obj);
                    }
                }
                sPools.Remove(pool);
                
                NotifyEditorDataChanged(CHANNEL_POOL_LIST_CHANGED, info);
            }
        }

        /// <summary>
        /// 记录对象借出
        /// </summary>
        public static void TrackAllocate(object pool, object obj)
        {
            if (pool == default || obj == default || !EnableTracking) return;
            
            if (!sPools.TryGetValue(pool, out var info)) return;
            
            var stackTrace = EnableStackTrace ? Environment.StackTrace : string.Empty;
            var activeInfo = new ActiveObjectInfo
            {
                Obj = obj,
                SpawnTime = Time.realtimeSinceStartup,
                StackTrace = stackTrace
            };
            
            info.ActiveObjects.Add(activeInfo);
            info.ActiveCount = info.ActiveObjects.Count;
            sObjectToPool[obj] = pool;
            
            if (info.ActiveCount > info.PeakCount)
            {
                info.PeakCount = info.ActiveCount;
            }
            
            if (EnableEventHistory)
            {
                RecordEvent(PoolEventType.Spawn, info.Name, obj, stackTrace);
            }
            
            NotifyEditorDataChanged(CHANNEL_POOL_ACTIVE_CHANGED, info);
        }

        /// <summary>
        /// 记录对象归还
        /// </summary>
        public static void TrackRecycle(object pool, object obj)
        {
            if (pool == default || obj == default || !EnableTracking) return;
            
            if (!sPools.TryGetValue(pool, out var info)) return;
            
            for (int i = info.ActiveObjects.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(info.ActiveObjects[i].Obj, obj))
                {
                    info.ActiveObjects.RemoveAt(i);
                    break;
                }
            }
            
            info.ActiveCount = info.ActiveObjects.Count;
            sObjectToPool.Remove(obj);
            
            if (EnableEventHistory)
            {
                var stackTrace = EnableStackTrace ? Environment.StackTrace : string.Empty;
                RecordEvent(PoolEventType.Return, info.Name, obj, stackTrace);
            }
            
            NotifyEditorDataChanged(CHANNEL_POOL_ACTIVE_CHANGED, info);
        }

        /// <summary>
        /// 更新池的总容量
        /// </summary>
        public static void UpdateTotalCount(object pool, int totalCount)
        {
            if (pool == default || !EnableTracking) return;
            
            if (sPools.TryGetValue(pool, out var info))
            {
                info.TotalCount = totalCount;
            }
        }

        /// <summary>
        /// 获取所有池的调试信息
        /// </summary>
        public static void GetAllPools(List<PoolDebugInfo> result)
        {
            result.Clear();
            foreach (var kvp in sPools)
            {
                result.Add(kvp.Value);
            }
        }

        /// <summary>
        /// 获取池数量
        /// </summary>
        public static int PoolCount => sPools.Count;

        /// <summary>
        /// 强制归还对象
        /// </summary>
        public static bool ForceReturn(object pool, object obj)
        {
            if (pool == default || obj == default) return false;
            
            string poolName = "Unknown";
            if (sPools.TryGetValue(pool, out var info))
            {
                poolName = info.Name;
            }
            
            var poolType = pool.GetType();
            var recycleMethod = poolType.GetMethod("Recycle");
            
            if (recycleMethod == default) return false;
            
            try
            {
                recycleMethod.Invoke(pool, new[] { obj });
                
                if (EnableEventHistory)
                {
                    RecordEvent(PoolEventType.Forced, poolName, obj, "ForceReturn");
                }
                
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PoolDebugger] 强制归还失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 记录事件到历史队列
        /// </summary>
        private static void RecordEvent(PoolEventType eventType, string poolName, object obj, string stackTrace)
        {
            while (sEventHistory.Count >= MAX_EVENT_HISTORY)
            {
                sEventHistory.Dequeue();
            }
            
            var objName = obj?.ToString() ?? "null";
            if (obj is UnityEngine.Object unityObj && unityObj != default)
            {
                objName = unityObj.name;
            }
            
            var evt = new PoolEvent
            {
                EventType = eventType,
                Timestamp = Time.realtimeSinceStartup,
                PoolName = poolName,
                ObjectName = objName,
                Source = ParseStackTraceSource(stackTrace),
                StackTrace = stackTrace,
                ObjRef = obj
            };
            
            sEventHistory.Enqueue(evt);
            
            NotifyEditorDataChanged(CHANNEL_POOL_EVENT_LOGGED, evt);
        }

        /// <summary>
        /// 解析堆栈追踪获取调用来源
        /// </summary>
        private static string ParseStackTraceSource(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "Unknown";
            
            var lines = stackTrace.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("System.Environment")) continue;
                if (line.Contains("PoolDebugger")) continue;
                if (line.Contains("PoolKit")) continue;
                if (line.Contains("SafePoolKit")) continue;
                if (line.Contains("SimplePoolKit")) continue;
                if (line.Contains("UnityEngine.")) continue;
                if (line.Contains("UnityEditor.")) continue;
                
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                if (trimmed.StartsWith("at "))
                {
                    trimmed = trimmed.Substring(3);
                }
                
                var parenIndex = trimmed.IndexOf('(');
                if (parenIndex > 0)
                {
                    trimmed = trimmed.Substring(0, parenIndex);
                }
                
                if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 3 && trimmed.Contains("."))
                {
                    return trimmed;
                }
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// 获取事件历史（最新的在前）
        /// </summary>
        public static void GetEventHistory(List<PoolEvent> result, PoolEventType? filterType = null, string poolName = null)
        {
            result.Clear();
            
            var events = sEventHistory.ToArray();
            for (int i = events.Length - 1; i >= 0; i--)
            {
                var evt = events[i];
                
                if (filterType != null && evt.EventType != filterType.Value) continue;
                if (!string.IsNullOrEmpty(poolName) && evt.PoolName != poolName) continue;
                
                result.Add(evt);
            }
        }
        
        /// <summary>
        /// 清空事件历史
        /// </summary>
        public static void ClearEventHistory()
        {
            sEventHistory.Clear();
        }
        
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
        public const float LEAK_THRESHOLD_SECONDS = 30f;
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
        public static void UnregisterPool(object pool) { }
        public static void TrackAllocate(object pool, object obj) { }
        public static void TrackRecycle(object pool, object obj) { }
        public static void UpdateTotalCount(object pool, int totalCount) { }
        public static void GetAllPools(List<PoolDebugInfo> result) => result.Clear();
        public static bool ForceReturn(object pool, object obj) => false;
        public static void GetEventHistory(List<PoolEvent> result, PoolEventType? filterType = null, string poolName = null) => result.Clear();
        public static void ClearEventHistory() { }
        public static void Clear() { }
#endif
    }
}
