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

        #region 批量加载 (AllAssets)

        /// <summary>
        /// 同步加载所有指定类型的资源（便利方法，内部自动管理 Loader 生命周期）
        /// </summary>
        /// <param name="path">资源路径（YooAsset: Bundle 内任意地址；Resources: 文件夹路径；Editor: Assets/ 路径）</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>所有匹配类型的资源数组</returns>
        public static T[] LoadAll<T>(string path) where T : Object
        {
            var handler = LoadAllAsset<T>(path);
            return handler?.GetAllAssetObjects<T>() ?? System.Array.Empty<T>();
        }

        /// <summary>
        /// 同步加载并获取批量资源句柄（需调用方管理生命周期：使用完毕后调用 handler.Release()）
        /// </summary>
        public static AllAssetsResHandler LoadAllAsset<T>(string path) where T : Object
        {
            var loader = sLoaderPool.Allocate();

            if (loader is not IAllAssetsLoader allLoader)
            {
                KitLogger.Error("[ResKit] 当前加载器不支持 LoadAll，请使用支持 IAllAssetsLoader 的加载池");
                loader.UnloadAndRecycle();
                return null;
            }

            var result = allLoader.LoadAll<T>(path);
            var handler = new AllAssetsResHandler
            {
                Path = path,
                AssetType = typeof(T),
                AllAssetObjects = result,
                Loader = loader,
                IsDone = true
            };
            handler.Retain();
            return handler;
        }

        /// <summary>
        /// 异步加载并获取批量资源句柄
        /// </summary>
        public static void LoadAllAssetAsync<T>(string path, Action<AllAssetsResHandler> onComplete) where T : Object
        {
            var loader = sLoaderPool.Allocate();

            if (loader is not IAllAssetsLoader allLoader)
            {
                KitLogger.Error("[ResKit] 当前加载器不支持 LoadAllAsync，请使用支持 IAllAssetsLoader 的加载池");
                loader.UnloadAndRecycle();
                onComplete?.Invoke(null);
                return;
            }

            var handler = new AllAssetsResHandler
            {
                Path = path,
                AssetType = typeof(T),
                Loader = loader
            };
            handler.Retain();

            allLoader.LoadAllAsync<T>(path, result =>
            {
                handler.AllAssetObjects = result;
                handler.IsDone = true;
                onComplete?.Invoke(handler);
                handler.InvokeLoadedCallbacks();
            });
        }

        #endregion

        #region 子资源加载 (SubAssets)

        /// <summary>
        /// 同步加载子资源并获取句柄（如 SpriteAtlas → Sprite）
        /// 使用完毕后调用 handler.Release()
        /// </summary>
        /// <example>
        /// var handler = ResKit.LoadSubAsset&lt;Sprite&gt;("Assets/GameRes/UIAtlas/login.spriteatlas");
        /// var sprite = handler.GetSubAssetObject&lt;Sprite&gt;("spriteName");
        /// // 使用完毕
        /// handler.Release();
        /// </example>
        public static SubAssetsResHandler LoadSubAsset<T>(string path) where T : Object
        {
            var loader = sLoaderPool.Allocate();

            if (loader is not ISubAssetsLoader subLoader)
            {
                KitLogger.Error("[ResKit] 当前加载器不支持 LoadSub，请使用支持 ISubAssetsLoader 的加载池");
                loader.UnloadAndRecycle();
                return null;
            }

            var result = subLoader.LoadSub<T>(path);
            var handler = new SubAssetsResHandler
            {
                Path = path,
                AssetType = typeof(T),
                AllAssetObjects = result.AllSubAssets as Object[] ?? ConvertToObjectArray(result.AllSubAssets),
                Loader = loader,
                IsDone = true
            };
            handler.Retain();
            return handler;
        }

        /// <summary>
        /// 异步加载子资源并获取句柄
        /// </summary>
        public static void LoadSubAssetAsync<T>(string path, Action<SubAssetsResHandler> onComplete) where T : Object
        {
            var loader = sLoaderPool.Allocate();

            if (loader is not ISubAssetsLoader subLoader)
            {
                KitLogger.Error("[ResKit] 当前加载器不支持 LoadSubAsync，请使用支持 ISubAssetsLoader 的加载池");
                loader.UnloadAndRecycle();
                onComplete?.Invoke(null);
                return;
            }

            var handler = new SubAssetsResHandler
            {
                Path = path,
                AssetType = typeof(T),
                Loader = loader
            };
            handler.Retain();

            subLoader.LoadSubAsync<T>(path, result =>
            {
                handler.AllAssetObjects = result.AllSubAssets as Object[] ?? ConvertToObjectArray(result.AllSubAssets);
                handler.IsDone = true;
                onComplete?.Invoke(handler);
                handler.InvokeLoadedCallbacks();
            });
        }

        private static Object[] ConvertToObjectArray<T>(T[] source) where T : Object
        {
            if (source is null) return System.Array.Empty<Object>();
            var result = new Object[source.Length];
            System.Array.Copy(source, result, source.Length);
            return result;
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 批量加载

        /// <summary>
        /// [UniTask] 异步加载并获取批量资源句柄
        /// </summary>
        public static async UniTask<AllAssetsResHandler> LoadAllAssetUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var loader = sLoaderPool.Allocate();

            var handler = new AllAssetsResHandler
            {
                Path = path,
                AssetType = typeof(T),
                Loader = loader
            };
            handler.Retain();

            Object[] assets;

            if (loader is IAllAssetsLoaderUniTask uniTaskLoader)
            {
                var result = await uniTaskLoader.LoadAllUniTaskAsync<T>(path, cancellationToken);
                assets = result as Object[] ?? ConvertToObjectArray(result);
            }
            else if (loader is IAllAssetsLoader allLoader)
            {
                var tcs = new UniTaskCompletionSource<T[]>();
                allLoader.LoadAllAsync<T>(path, r => tcs.TrySetResult(r));
                var result = await tcs.Task.AttachExternalCancellation(cancellationToken);
                assets = result as Object[] ?? ConvertToObjectArray(result);
            }
            else
            {
                KitLogger.Error("[ResKit] 当前加载器不支持 LoadAllUniTaskAsync");
                handler.Release();
                return null;
            }

            handler.AllAssetObjects = assets;
            handler.IsDone = true;
            handler.InvokeLoadedCallbacks();
            return handler;
        }

        /// <summary>
        /// [UniTask] 异步加载子资源并获取句柄
        /// </summary>
        public static async UniTask<SubAssetsResHandler> LoadSubAssetUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var loader = sLoaderPool.Allocate();

            var handler = new SubAssetsResHandler
            {
                Path = path,
                AssetType = typeof(T),
                Loader = loader
            };
            handler.Retain();

            Object[] assets;

            if (loader is ISubAssetsLoaderUniTask uniTaskLoader)
            {
                var result = await uniTaskLoader.LoadSubUniTaskAsync<T>(path, cancellationToken);
                assets = result.AllSubAssets as Object[] ?? ConvertToObjectArray(result.AllSubAssets);
            }
            else if (loader is ISubAssetsLoader subLoader)
            {
                var tcs = new UniTaskCompletionSource<SubAssetsResult<T>>();
                subLoader.LoadSubAsync<T>(path, r => tcs.TrySetResult(r));
                var result = await tcs.Task.AttachExternalCancellation(cancellationToken);
                assets = result.AllSubAssets as Object[] ?? ConvertToObjectArray(result.AllSubAssets);
            }
            else
            {
                KitLogger.Error("[ResKit] 当前加载器不支持 LoadSubUniTaskAsync");
                handler.Release();
                return null;
            }

            handler.AllAssetObjects = assets;
            handler.IsDone = true;
            handler.InvokeLoadedCallbacks();
            return handler;
        }

        #endregion
#endif
    }
}
