#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 编辑器调试器 - 通过反射访问运行时数据
    /// </summary>
    public static class ResDebugger
    {
        #region 事件通道常量

        /// <summary>
        /// 资源列表变化事件通道
        /// </summary>
        public const string CHANNEL_RES_LIST_CHANGED = "ResKit.ResListChanged";

        /// <summary>
        /// 资源卸载事件通道
        /// </summary>
        public const string CHANNEL_RES_UNLOADED = "ResKit.ResUnloaded";

        #endregion

        /// <summary>
        /// 资源来源
        /// </summary>
        public enum ResSource
        {
            ResKit,     // 通过 ResKit.LoadAsset 加载
            Loader      // 通过底层 Loader 直接加载（如 UIKit）
        }

        /// <summary>
        /// 资源信息快照
        /// </summary>
        public readonly struct ResInfo
        {
            public readonly string Path;
            public readonly string TypeName;
            public readonly int RefCount;
            public readonly bool IsDone;
            public readonly ResSource Source;

            public ResInfo(string path, string typeName, int refCount, bool isDone, ResSource source)
            {
                Path = path;
                TypeName = typeName;
                RefCount = refCount;
                IsDone = isDone;
                Source = source;
            }
        }
        
        /// <summary>
        /// 资源卸载历史记录
        /// </summary>
        public readonly struct UnloadRecord
        {
            public readonly string Path;
            public readonly string TypeName;
            public readonly DateTime UnloadTime;
            public readonly string StackTrace;

            public UnloadRecord(string path, string typeName, DateTime unloadTime, string stackTrace)
            {
                Path = path;
                TypeName = typeName;
                UnloadTime = unloadTime;
                StackTrace = stackTrace;
            }
        }

        private static FieldInfo sCacheField;
        private static bool sFieldCached;
        
        // 卸载历史记录
        private static readonly List<UnloadRecord> sUnloadHistory = new(128);
        private static readonly HashSet<string> sPreviousLoadedPaths = new();
        private const int MAX_HISTORY_COUNT = 100;
        
        // 上次资源数量（用于检测变化）
        private static int sLastLoadedCount;

        /// <summary>
        /// 获取卸载历史记录
        /// </summary>
        public static IReadOnlyList<UnloadRecord> GetUnloadHistory() => sUnloadHistory;
        
        /// <summary>
        /// 清空卸载历史记录
        /// </summary>
        public static void ClearUnloadHistory()
        {
            sUnloadHistory.Clear();
            sPreviousLoadedPaths.Clear();
        }
        
        /// <summary>
        /// 检测并记录卸载的资源（在 Update 中调用）
        /// </summary>
        public static void DetectUnloadedAssets()
        {
            if (!EditorApplication.isPlaying) return;
            
            var currentLoaded = GetLoadedAssets();
            var currentPaths = new HashSet<string>();
            
            foreach (var asset in currentLoaded)
            {
                currentPaths.Add(GetAssetKey(asset.Path, asset.TypeName));
            }
            
            bool hasUnloaded = false;
            
            // 检测哪些资源被卸载了
            foreach (var prevPath in sPreviousLoadedPaths)
            {
                if (!currentPaths.Contains(prevPath))
                {
                    // 资源被卸载了，记录历史
                    var parts = prevPath.Split('|');
                    var path = parts.Length > 0 ? parts[0] : prevPath;
                    var typeName = parts.Length > 1 ? parts[1] : "Unknown";
                    
                    var stackTrace = GetSimplifiedStackTrace();
                    
                    var record = new UnloadRecord(
                        path,
                        typeName,
                        DateTime.Now,
                        stackTrace
                    );
                    sUnloadHistory.Insert(0, record);
                    
                    // 限制历史记录数量
                    if (sUnloadHistory.Count > MAX_HISTORY_COUNT)
                    {
                        sUnloadHistory.RemoveAt(sUnloadHistory.Count - 1);
                    }
                    
                    hasUnloaded = true;
                    
                    // 通知编辑器资源卸载
                    EditorDataBridge.NotifyDataChanged(CHANNEL_RES_UNLOADED, record);
                }
            }
            
            // 更新当前加载的资源集合
            sPreviousLoadedPaths.Clear();
            foreach (var path in currentPaths)
            {
                sPreviousLoadedPaths.Add(path);
            }
            
            // 检测资源数量变化
            int currentCount = currentLoaded.Count;
            if (currentCount != sLastLoadedCount || hasUnloaded)
            {
                sLastLoadedCount = currentCount;
                // 通知编辑器资源列表变化
                EditorDataBridge.NotifyDataChanged(CHANNEL_RES_LIST_CHANGED, currentCount);
            }
        }
        
        private static string GetAssetKey(string path, string typeName) => $"{path}|{typeName}";
        
        private static string GetSimplifiedStackTrace()
        {
            var fullTrace = Environment.StackTrace;
            var lines = fullTrace.Split('\n');
            var result = new System.Text.StringBuilder();
            int count = 0;
            
            foreach (var line in lines)
            {
                // 跳过系统调用和编辑器调用
                if (line.Contains("ResDebugger") || 
                    line.Contains("EditorApplication") ||
                    line.Contains("UnityEditor") ||
                    line.Contains("System.Environment"))
                    continue;
                
                // 只保留用户代码相关的堆栈
                if (line.Contains("YokiFrame") || line.Contains("Assets/"))
                {
                    result.AppendLine(line.Trim());
                    count++;
                    if (count >= 5) break; // 最多保留5行
                }
            }
            
            return result.Length > 0 ? result.ToString() : "无可用堆栈信息";
        }

        /// <summary>
        /// 获取当前已加载的资源列表（包括 ResKit 缓存和底层 Loader 追踪）
        /// </summary>
        public static List<ResInfo> GetLoadedAssets()
        {
            var result = new List<ResInfo>();
            
            if (!EditorApplication.isPlaying) return result;

            // 1. 从 ResKit 缓存获取
            var cache = GetAssetCache();
            if (cache != null)
            {
                foreach (var kvp in cache)
                {
                    var handler = kvp.Value;
                    result.Add(new ResInfo(
                        handler.Path,
                        handler.AssetType?.Name ?? "Unknown",
                        handler.RefCount,
                        handler.IsDone,
                        ResSource.ResKit
                    ));
                }
            }

            // 2. 从底层 Loader 追踪获取（排除已在 ResKit 缓存中的）
            var tracked = ResLoadTracker.GetTrackedAssets();
            if (tracked != null)
            {
                foreach (var kvp in tracked)
                {
                    var asset = kvp.Value;
                    // 检查是否已在 ResKit 缓存中（避免重复）
                    if (!IsInResKitCache(asset.Path, asset.AssetType, cache))
                    {
                        result.Add(new ResInfo(
                            asset.Path,
                            asset.AssetType?.Name ?? "Unknown",
                            1, // 底层 Loader 没有引用计数概念
                            asset.IsLoaded,
                            ResSource.Loader
                        ));
                    }
                }
            }

            return result;
        }

        private static bool IsInResKitCache(string path, Type assetType, Dictionary<ResCacheKey, ResHandler> cache)
        {
            if (cache == null || string.IsNullOrEmpty(path)) return false;
            var key = new ResCacheKey(assetType, path);
            return cache.ContainsKey(key);
        }

        /// <summary>
        /// 获取已加载资源数量
        /// </summary>
        public static int GetLoadedCount()
        {
            if (!EditorApplication.isPlaying) return 0;
            
            int count = 0;
            
            var cache = GetAssetCache();
            if (cache != null) count += cache.Count;
            
            var tracked = ResLoadTracker.GetTrackedAssets();
            if (tracked != null)
            {
                foreach (var kvp in tracked)
                {
                    if (!IsInResKitCache(kvp.Value.Path, kvp.Value.AssetType, cache))
                    {
                        count++;
                    }
                }
            }
            
            return count;
        }

        /// <summary>
        /// 获取总引用计数
        /// </summary>
        public static int GetTotalRefCount()
        {
            if (!EditorApplication.isPlaying) return 0;
            
            var cache = GetAssetCache();
            if (cache == null) return 0;

            int total = 0;
            foreach (var handler in cache.Values)
            {
                total += handler.RefCount;
            }
            return total;
        }

        private static Dictionary<ResCacheKey, ResHandler> GetAssetCache()
        {
            if (!sFieldCached)
            {
                var type = typeof(ResKit);
                sCacheField = type.GetField("sAssetCache", BindingFlags.NonPublic | BindingFlags.Static);
                sFieldCached = true;
            }

            return sCacheField?.GetValue(null) as Dictionary<ResCacheKey, ResHandler>;
        }
    }
}
#endif
