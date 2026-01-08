using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
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
    /// 资源缓存 Key（避免字符串拼接 GC）
    /// </summary>
    public readonly struct ResCacheKey : IEquatable<ResCacheKey>
    {
        public readonly Type AssetType;
        public readonly string Path;

        public ResCacheKey(Type assetType, string path)
        {
            AssetType = assetType;
            Path = path;
        }

        public bool Equals(ResCacheKey other) => AssetType == other.AssetType && Path == other.Path;
        public override bool Equals(object obj) => obj is ResCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(AssetType, Path);
    }

    /// <summary>
    /// 资源管理工具
    /// </summary>
    public static class ResKit
    {
#if YOKIFRAME_UNITASK_SUPPORT
        private static IResLoaderPool sLoaderPool = new DefaultResLoaderUniTaskPool();
        private static IRawFileLoaderPool sRawFileLoaderPool = new DefaultRawFileLoaderUniTaskPool();
        private static ISceneResLoaderPool sSceneLoaderPool = new DefaultSceneResLoaderUniTaskPool();
#else
        private static IResLoaderPool sLoaderPool = new DefaultResLoaderPool();
        private static IRawFileLoaderPool sRawFileLoaderPool = new DefaultRawFileLoaderPool();
        private static ISceneResLoaderPool sSceneLoaderPool = new DefaultSceneResLoaderPool();
#endif
        private static readonly Dictionary<ResCacheKey, ResHandler> sAssetCache = new();

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

        /// <summary>
        /// 设置自定义原始文件加载池（用于 YooAsset 等扩展）
        /// </summary>
        public static void SetRawFileLoaderPool(IRawFileLoaderPool pool)
        {
            sRawFileLoaderPool = pool;
            KitLogger.Log($"[ResKit] 原始文件加载池已切换为: {pool.GetType().Name}");
        }

        /// <summary>
        /// 获取当前原始文件加载池
        /// </summary>
        public static IRawFileLoaderPool GetRawFileLoaderPool() => sRawFileLoaderPool;

        /// <summary>
        /// 设置自定义场景加载池（用于 YooAsset 等扩展）
        /// </summary>
        public static void SetSceneLoaderPool(ISceneResLoaderPool pool)
        {
            sSceneLoaderPool = pool;
            KitLogger.Log($"[ResKit] 场景加载池已切换为: {pool.GetType().Name}");
        }

        /// <summary>
        /// 获取当前场景加载池
        /// </summary>
        public static ISceneResLoaderPool GetSceneLoaderPool() => sSceneLoaderPool;

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
            var key = new ResCacheKey(typeof(T), path);

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

            var key = new ResCacheKey(handler.AssetType, handler.Path);
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

        #region 原始文件加载

        /// <summary>
        /// 同步加载原始文件文本
        /// </summary>
        /// <param name="path">资源路径（Resources 方式需放在 Resources 文件夹下）</param>
        /// <returns>文件文本内容，加载失败返回 null</returns>
        public static string LoadRawFileText(string path)
        {
            var loader = sRawFileLoaderPool.Allocate();
            var text = loader.LoadRawFileText(path);
            loader.UnloadAndRecycle();
            return text;
        }

        /// <summary>
        /// 同步加载原始文件字节数据
        /// </summary>
        /// <param name="path">资源路径（Resources 方式需放在 Resources 文件夹下）</param>
        /// <returns>文件字节数据，加载失败返回 null</returns>
        public static byte[] LoadRawFileData(string path)
        {
            var loader = sRawFileLoaderPool.Allocate();
            var data = loader.LoadRawFileData(path);
            loader.UnloadAndRecycle();
            return data;
        }

        /// <summary>
        /// 异步加载原始文件文本
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="onComplete">完成回调</param>
        public static void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            var loader = sRawFileLoaderPool.Allocate();
            loader.LoadRawFileTextAsync(path, text =>
            {
                onComplete?.Invoke(text);
                loader.UnloadAndRecycle();
            });
        }

        /// <summary>
        /// 异步加载原始文件字节数据
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="onComplete">完成回调</param>
        public static void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            var loader = sRawFileLoaderPool.Allocate();
            loader.LoadRawFileDataAsync(path, data =>
            {
                onComplete?.Invoke(data);
                loader.UnloadAndRecycle();
            });
        }

        /// <summary>
        /// 获取原始文件的完整路径（用于需要直接访问文件的场景）
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <returns>完整文件路径，Resources 方式返回 null</returns>
        public static string GetRawFilePath(string path)
        {
            var loader = sRawFileLoaderPool.Allocate();
            var filePath = loader.GetRawFilePath(path);
            loader.UnloadAndRecycle();
            return filePath;
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
        /// [UniTask] 异步加载并获取句柄
        /// </summary>
        public static UniTask<ResHandler> LoadAssetUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var key = new ResCacheKey(typeof(T), path);

            if (sAssetCache.TryGetValue(key, out var handler))
            {
                handler.Retain();
                return UniTask.FromResult(handler);
            }

            return LoadAssetUniTaskAsyncInternal<T>(path, key, cancellationToken);
        }

        private static async UniTask<ResHandler> LoadAssetUniTaskAsyncInternal<T>(string path, ResCacheKey key, CancellationToken cancellationToken) where T : Object
        {
            var handler = SafePoolKit<ResHandler>.Instance.Allocate();
            handler.Path = path;
            handler.AssetType = typeof(T);
            handler.Loader = sLoaderPool.Allocate();
            handler.Retain();

            sAssetCache.Add(key, handler);

            // 使用 UniTaskCompletionSource 包装回调
            var tcs = new UniTaskCompletionSource<Object>();
            
            handler.Loader.LoadAsync<T>(path, asset =>
            {
                tcs.TrySetResult(asset);
            });

            // 等待加载完成或取消
            var asset = await tcs.Task.AttachExternalCancellation(cancellationToken);
            
            handler.Asset = asset;
            handler.IsDone = true;

            if (asset == null)
            {
                KitLogger.Error($"[ResKit] 资源加载失败: {path}");
            }

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

        #region UniTask 原始文件加载

        /// <summary>
        /// [UniTask] 异步加载原始文件文本
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件文本内容</returns>
        public static async UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var loader = sRawFileLoaderPool.Allocate();
            
            if (loader is IRawFileLoaderUniTask uniTaskLoader)
            {
                var text = await uniTaskLoader.LoadRawFileTextUniTaskAsync(path, cancellationToken);
                loader.UnloadAndRecycle();
                return text;
            }

            // 回退到回调方式
            var tcs = new UniTaskCompletionSource<string>();
            loader.LoadRawFileTextAsync(path, text => tcs.TrySetResult(text));
            var result = await tcs.Task.AttachExternalCancellation(cancellationToken);
            loader.UnloadAndRecycle();
            return result;
        }

        /// <summary>
        /// [UniTask] 异步加载原始文件字节数据
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件字节数据</returns>
        public static async UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var loader = sRawFileLoaderPool.Allocate();
            
            if (loader is IRawFileLoaderUniTask uniTaskLoader)
            {
                var data = await uniTaskLoader.LoadRawFileDataUniTaskAsync(path, cancellationToken);
                loader.UnloadAndRecycle();
                return data;
            }

            // 回退到回调方式
            var tcs = new UniTaskCompletionSource<byte[]>();
            loader.LoadRawFileDataAsync(path, data => tcs.TrySetResult(data));
            var result = await tcs.Task.AttachExternalCancellation(cancellationToken);
            loader.UnloadAndRecycle();
            return result;
        }

        #endregion
#endif
    }
}
