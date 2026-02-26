using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 资源加载追踪器 - 追踪所有通过底层 Loader 加载的资源
    /// 编辑器模式下用于监控，运行时为空实现（零开销）
    /// </summary>
    public static class ResLoadTracker
    {
        /// <summary>
        /// 追踪的资源信息
        /// </summary>
        public class TrackedAsset
        {
            public string Path;
            public Type AssetType;
            public Object Asset;
            public IResLoader Loader;
            public bool IsLoaded;
        }

        // 使用 Loader 实例作为 key，因为每个 Loader 只加载一个资源
        private static readonly Dictionary<IResLoader, TrackedAsset> sTrackedAssets = new();

#if UNITY_EDITOR
        /// <summary>
        /// 记录资源加载
        /// </summary>
        public static void OnLoad(IResLoader loader, string path, Type assetType, Object asset)
        {
            if (loader is null) return;
            
            if (!sTrackedAssets.TryGetValue(loader, out var tracked))
            {
                tracked = new TrackedAsset();
                sTrackedAssets[loader] = tracked;
            }
            
            tracked.Path = path;
            tracked.AssetType = assetType;
            tracked.Asset = asset;
            tracked.Loader = loader;
            tracked.IsLoaded = asset != null;
        }

        /// <summary>
        /// 记录资源卸载
        /// </summary>
        public static void OnUnload(IResLoader loader)
        {
            if (loader is null) return;
            sTrackedAssets.Remove(loader);
        }

        /// <summary>
        /// 获取所有追踪的资源
        /// </summary>
        public static IReadOnlyDictionary<IResLoader, TrackedAsset> GetTrackedAssets() => sTrackedAssets;

        /// <summary>
        /// 清空追踪数据
        /// </summary>
        public static void Clear() => sTrackedAssets.Clear();
#else
        // 运行时空实现
        public static void OnLoad(IResLoader loader, string path, Type assetType, Object asset) { }
        public static void OnUnload(IResLoader loader) { }
        public static IReadOnlyDictionary<IResLoader, TrackedAsset> GetTrackedAssets() => null;
        public static void Clear() { }
#endif
    }
}
