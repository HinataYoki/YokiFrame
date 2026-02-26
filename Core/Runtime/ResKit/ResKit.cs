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
