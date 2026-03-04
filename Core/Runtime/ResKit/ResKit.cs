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

        /// <summary>
        /// 等待加载完成的回调链（用委托链代替 List，常见情况零堆分配）
        /// </summary>
        private Action<ResHandler> mOnLoaded;

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

        /// <summary>
        /// 添加加载完成回调。若已完成则立即回调，否则排队等待。
        /// </summary>
        public void AddLoadedCallback(Action<ResHandler> callback)
        {
            if (callback is null) return;

            if (IsDone)
            {
                callback.Invoke(this);
                return;
            }

            mOnLoaded += callback;
        }

        /// <summary>
        /// 加载完成后，通知所有等待者
        /// </summary>
        public void InvokeLoadedCallbacks()
        {
            var callbacks = mOnLoaded;
            mOnLoaded = null;
            callbacks?.Invoke(this);
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
            mOnLoaded = null;
        }
    }

    /// <summary>
    /// 批量资源句柄 — 管理 AllAssets 加载的生命周期
    /// </summary>
    /// <remarks>
    /// 调用方通过 Retain()/Release() 管理引用计数。
    /// 引用归零后自动回收底层 Loader（释放 YooAsset AllAssetsHandle）。
    /// </remarks>
    public class AllAssetsResHandler
    {
        public string Path;
        public Type AssetType;
        public Object[] AllAssetObjects;
        public IResLoader Loader;
        public int RefCount;
        public bool IsDone;

        private Action<AllAssetsResHandler> mOnLoaded;

        public void Retain() => RefCount++;

        public void Release()
        {
            RefCount--;
            if (RefCount <= 0)
            {
                Loader?.UnloadAndRecycle();
                Loader = null;
                AllAssetObjects = null;
                Path = null;
                AssetType = null;
                mOnLoaded = null;
            }
        }

        /// <summary>
        /// 获取所有指定类型的资源
        /// </summary>
        public T[] GetAllAssetObjects<T>() where T : Object
        {
            if (AllAssetObjects is null) return Array.Empty<T>();

            var result = new System.Collections.Generic.List<T>(AllAssetObjects.Length);
            foreach (var obj in AllAssetObjects)
            {
                if (obj is T typed)
                    result.Add(typed);
            }
            return result.ToArray();
        }

        public void AddLoadedCallback(Action<AllAssetsResHandler> callback)
        {
            if (callback is null) return;
            if (IsDone) { callback.Invoke(this); return; }
            mOnLoaded += callback;
        }

        public void InvokeLoadedCallbacks()
        {
            var callbacks = mOnLoaded;
            mOnLoaded = null;
            callbacks?.Invoke(this);
        }
    }

    /// <summary>
    /// 子资源句柄 — 管理 SubAssets 加载的生命周期
    /// </summary>
    /// <remarks>
    /// 调用方通过 Retain()/Release() 管理引用计数。
    /// 引用归零后自动回收底层 Loader（释放 YooAsset SubAssetsHandle）。
    /// </remarks>
    public class SubAssetsResHandler
    {
        public string Path;
        public Type AssetType;
        public Object[] AllAssetObjects;
        public IResLoader Loader;
        public int RefCount;
        public bool IsDone;

        private Action<SubAssetsResHandler> mOnLoaded;

        public void Retain() => RefCount++;

        public void Release()
        {
            RefCount--;
            if (RefCount <= 0)
            {
                Loader?.UnloadAndRecycle();
                Loader = null;
                AllAssetObjects = null;
                Path = null;
                AssetType = null;
                mOnLoaded = null;
            }
        }

        /// <summary>
        /// 按名称获取子资源对象
        /// </summary>
        /// <param name="name">子资源名称</param>
        /// <typeparam name="T">子资源类型</typeparam>
        /// <returns>匹配的子资源，未找到返回 null</returns>
        public T GetSubAssetObject<T>(string name) where T : Object
        {
            if (AllAssetObjects is null) return null;

            foreach (var obj in AllAssetObjects)
            {
                if (obj is T typed && typed.name == name)
                    return typed;
            }
            return null;
        }

        /// <summary>
        /// 获取所有指定类型的子资源
        /// </summary>
        public T[] GetAllSubAssetObjects<T>() where T : Object
        {
            if (AllAssetObjects is null) return Array.Empty<T>();

            var result = new List<T>(AllAssetObjects.Length);
            foreach (var obj in AllAssetObjects)
            {
                if (obj is T typed)
                    result.Add(typed);
            }
            return result.ToArray();
        }

        public void AddLoadedCallback(Action<SubAssetsResHandler> callback)
        {
            if (callback is null) return;
            if (IsDone) { callback.Invoke(this); return; }
            mOnLoaded += callback;
        }

        public void InvokeLoadedCallbacks()
        {
            var callbacks = mOnLoaded;
            mOnLoaded = null;
            callbacks?.Invoke(this);
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
    public static partial class ResKit
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

        #region 卸载

        /// <summary>
        /// 卸载资源
        /// </summary>
        internal static void UnloadAsset(ResHandler handler)
        {
            if (handler is null) return;

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
        public static string GetRawFilePath(string path)
        {
            var loader = sRawFileLoaderPool.Allocate();
            var filePath = loader.GetRawFilePath(path);
            loader.UnloadAndRecycle();
            return filePath;
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 原始文件加载

        /// <summary>
        /// [UniTask] 异步加载原始文件文本
        /// </summary>
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
