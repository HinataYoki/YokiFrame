using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 资源句柄 - 管理单个资源的引用计数
    /// </summary>
    public class ResHandler : IPoolable
    {
        public string Path;
        public Type AssetType;
        public Object Asset;
        public IResLoader Loader;
        public int RefCount;
        public bool IsDone;

        public bool IsRecycled { get; set; }

        public void Retain() => RefCount++;

        public void Release()
        {
            RefCount--;
            if (RefCount <= 0)
            {
                ResKit.UnloadAsset(this);
            }
        }

        public void OnRecycled()
        {
            Path = null;
            AssetType = null;
            Asset = null;
            Loader?.UnloadAndRecycle();
            Loader = null;
            RefCount = 0;
            IsDone = false;
        }
    }

    /// <summary>
    /// 资源管理工具
    /// </summary>
    public static class ResKit
    {
        private static IResLoaderPool sLoaderPool = new DefaultResLoaderPool();
        private static readonly Dictionary<string, ResHandler> sAssetCache = new();

        /// <summary>
        /// 设置自定义加载池（用于 YooAsset 等扩展）
        /// </summary>
        public static void SetLoaderPool(IResLoaderPool pool)
        {
            sLoaderPool = pool;
            KitLogger.Log($"[ResKit] 加载池已切换为: {pool.GetType().Name}");
        }

        /// <summary>
        /// 获取当前加载池
        /// </summary>
        public static IResLoaderPool GetLoaderPool() => sLoaderPool;

        #region 同步加载

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public static T Load<T>(string path) where T : Object
        {
            var handler = LoadAsset<T>(path);
            return handler?.Asset as T;
        }

        /// <summary>
        /// 同步加载并获取句柄（需要手动管理引用计数）
        /// </summary>
        public static ResHandler LoadAsset<T>(string path) where T : Object
        {
            var key = GetCacheKey<T>(path);

            if (sAssetCache.TryGetValue(key, out var handler))
            {
                handler.Retain();
                return handler;
            }

            handler = SafePoolKit<ResHandler>.Instance.Allocate();
            handler.Path = path;
            handler.AssetType = typeof(T);
            handler.Loader = sLoaderPool.Allocate();
            handler.Asset = handler.Loader.Load<T>(path);
            handler.IsDone = true;
            handler.Retain();

            if (handler.Asset == null)
            {
                KitLogger.Error($"[ResKit] 资源加载失败: {path}");
                SafePoolKit<ResHandler>.Instance.Recycle(handler);
                return null;
            }

            sAssetCache.Add(key, handler);
            return handler;
        }

        #endregion

        #region 异步加载

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public static void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            LoadAssetAsync<T>(path, handler =>
            {
                onComplete?.Invoke(handler?.Asset as T);
            });
        }

        /// <summary>
        /// 异步加载并获取句柄
        /// </summary>
        public static void LoadAssetAsync<T>(string path, Action<ResHandler> onComplete) where T : Object
        {
            var key = GetCacheKey<T>(path);

            if (sAssetCache.TryGetValue(key, out var handler))
            {
                handler.Retain();
                onComplete?.Invoke(handler);
                return;
            }

            handler = SafePoolKit<ResHandler>.Instance.Allocate();
            handler.Path = path;
            handler.AssetType = typeof(T);
            handler.Loader = sLoaderPool.Allocate();
            handler.Retain();

            sAssetCache.Add(key, handler);

            handler.Loader.LoadAsync<T>(path, asset =>
            {
                handler.Asset = asset;
                handler.IsDone = true;

                if (asset == null)
                {
                    KitLogger.Error($"[ResKit] 资源加载失败: {path}");
                }

                onComplete?.Invoke(handler);
            });
        }

        #endregion

        #region 实例化

        /// <summary>
        /// 实例化预制体
        /// </summary>
        public static GameObject Instantiate(string path, Transform parent = null)
        {
            var prefab = Load<GameObject>(path);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        /// <summary>
        /// 异步实例化预制体
        /// </summary>
        public static void InstantiateAsync(string path, Action<GameObject> onComplete, Transform parent = null)
        {
            LoadAsync<GameObject>(path, prefab =>
            {
                var instance = prefab != null ? Object.Instantiate(prefab, parent) : null;
                onComplete?.Invoke(instance);
            });
        }

        #endregion

        #region 卸载

        /// <summary>
        /// 卸载资源
        /// </summary>
        internal static void UnloadAsset(ResHandler handler)
        {
            if (handler == null) return;

            var key = GetCacheKey(handler.AssetType, handler.Path);
            sAssetCache.Remove(key);
            SafePoolKit<ResHandler>.Instance.Recycle(handler);
        }

        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public static void ClearAll()
        {
            foreach (var handler in sAssetCache.Values)
            {
                handler.Loader?.UnloadAndRecycle();
            }
            sAssetCache.Clear();
        }

        #endregion

        private static string GetCacheKey<T>(string path) => $"{typeof(T).FullName}:{path}";
        private static string GetCacheKey(Type type, string path) => $"{type.FullName}:{path}";
    }
}
