using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 资源管理工具 - 加载方法
    /// </summary>
    public static partial class ResKit
    {
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
            var key = new ResCacheKey(typeof(T), path);

            if (sAssetCache.TryGetValue(key, out var handler))
            {
                handler.Retain();

                // 命中了异步加载中的未完成缓存，用临时 loader 同步补全
                // 不能用 handler.Loader（异步 handle 仍在进行中，覆盖会导致泄漏）
                if (!handler.IsDone)
                {
                    var tempLoader = sLoaderPool.Allocate();
                    handler.Asset = tempLoader.Load<T>(path);
                    handler.IsDone = true;
                    tempLoader.UnloadAndRecycle();
                    handler.InvokeLoadedCallbacks();
                }

                return handler;
            }

            handler = SafePoolKit<ResHandler>.Instance.Allocate();
            handler.Path = path;
            handler.AssetType = typeof(T);
            handler.Loader = sLoaderPool.Allocate();
            handler.Asset = handler.Loader.Load<T>(path);
            handler.IsDone = true;
            handler.Retain();

            if (handler.Asset == default)
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
            var key = new ResCacheKey(typeof(T), path);

            if (sAssetCache.TryGetValue(key, out var handler))
            {
                handler.Retain();

                // 已完成直接回调，未完成则排队等待
                if (handler.IsDone)
                    onComplete?.Invoke(handler);
                else
                    handler.AddLoadedCallback(onComplete);

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

                if (asset == default)
                {
                    KitLogger.Error($"[ResKit] 资源加载失败: {path}");
                }

                onComplete?.Invoke(handler);
                handler.InvokeLoadedCallbacks();
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

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 异步加载

        /// <summary>
        /// [UniTask] 异步加载资源
        /// </summary>
        public static async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var handler = await LoadAssetUniTaskAsync<T>(path, cancellationToken);
            return handler?.Asset as T;
        }

        /// <summary>
        /// [UniTask] 异步加载并获取句柄（优先使用原生 UniTask 加载器）
        /// </summary>
        public static async UniTask<ResHandler> LoadAssetUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var key = new ResCacheKey(typeof(T), path);

            // 缓存命中
            if (sAssetCache.TryGetValue(key, out var handler))
            {
                handler.Retain();

                if (handler.IsDone)
                    return handler;

                // 未完成，排队等待
                var waitTcs = new UniTaskCompletionSource<ResHandler>();
                handler.AddLoadedCallback(h => waitTcs.TrySetResult(h));
                return await waitTcs.Task.AttachExternalCancellation(cancellationToken);
            }

            // 缓存未命中 — 新建 handler
            handler = SafePoolKit<ResHandler>.Instance.Allocate();
            handler.Path = path;
            handler.AssetType = typeof(T);
            handler.Loader = sLoaderPool.Allocate();
            handler.Retain();

            sAssetCache.Add(key, handler);

            Object asset;

            // 优先用原生 UniTask 加载器（零额外分配）
            if (handler.Loader is IResLoaderUniTask uniTaskLoader)
            {
                asset = await uniTaskLoader.LoadUniTaskAsync<T>(path, cancellationToken);
            }
            else
            {
                // 回退：用 TCS 包装回调
                var tcs = new UniTaskCompletionSource<Object>();
                handler.Loader.LoadAsync<T>(path, a => tcs.TrySetResult(a));
                asset = await tcs.Task.AttachExternalCancellation(cancellationToken);
            }

            handler.Asset = asset;
            handler.IsDone = true;

            if (asset == default)
            {
                KitLogger.Error($"[ResKit] 资源加载失败: {path}");
            }

            handler.InvokeLoadedCallbacks();
            return handler;
        }

        /// <summary>
        /// [UniTask] 异步实例化预制体
        /// </summary>
        public static async UniTask<GameObject> InstantiateUniTaskAsync(string path, Transform parent = null, CancellationToken cancellationToken = default)
        {
            var prefab = await LoadUniTaskAsync<GameObject>(path, cancellationToken);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        #endregion
#endif
    }
}
