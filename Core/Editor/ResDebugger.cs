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

        private static FieldInfo sCacheField;
        private static bool sFieldCached;

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
