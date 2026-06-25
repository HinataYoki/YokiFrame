using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>
        /// 释放资源句柄的一次引用。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="handle">需要释放的资源句柄。</param>
        public static void Release<T>(ResHandle<T> handle) where T : class
        {
            if (handle == null)
                return;

            var shouldRelease = false;
            T assetToRelease = null;
            lock (sLock)
            {
                if (handle.RefCount <= 0 || handle.Asset == null)
                    return;

                handle.RefCount--;
                if (handle.RefCount > 0)
                    return;

                var key = new ResCacheKey(typeof(T), handle.Path);
                sAssetCache.Remove(key);
                AddUnloadRecordLocked(handle.Path, typeof(T).Name, handle.ProviderName);
                assetToRelease = handle.Asset;
                shouldRelease = true;
            }

            if (shouldRelease && sProvider != null)
                sProvider.Release(assetToRelease);

            handle.Invalidate();
        }

        /// <summary>
        /// 直接释放资源对象。
        /// </summary>
        /// <param name="asset">需要释放的资源对象。</param>
        public static void Release(object asset)
        {
            if (asset == null)
                return;

            lock (sLock)
            {
                foreach (var kvp in sAssetCache)
                {
                    if (kvp.Value is IResHandleReleaser releaser && releaser.TryReleaseObject(asset))
                        return;
                }
            }

            if (sProvider != null)
                sProvider.Release(asset);
        }

        /// <summary>
        /// 清空所有已缓存资源，并释放 Provider 中的资源对象。
        /// </summary>
        public static void ClearAll()
        {
            List<object> assets = null;
            lock (sLock)
            {
                if (sAssetCache.Count > 0)
                {
                    assets = new List<object>(sAssetCache.Count);
                    foreach (var kvp in sAssetCache)
                    {
                        if (kvp.Value is not IResHandleDebugView handle)
                            continue;

                        assets.Add(handle.AssetObject);
                        var typeName = handle.AssetType != null ? handle.AssetType.Name : "Unknown";
                        AddUnloadRecordLocked(handle.Path, typeName, handle.ProviderName);
                        if (kvp.Value is IResHandleInvalidator invalidator)
                            invalidator.Invalidate();
                    }
                }

                sAssetCache.Clear();
            }

            if (assets == null || sProvider == null)
                return;

            for (var i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                    sProvider.Release(assets[i]);
            }
        }
        private static void AddUnloadRecordLocked(string path, string typeName, string providerName)
        {
            while (sUnloadHistory.Count >= MAX_UNLOAD_HISTORY)
                sUnloadHistory.Dequeue();

            sUnloadHistory.Enqueue(new ResUnloadRecord
            {
                Path = path,
                TypeName = typeName,
                ProviderName = providerName,
                UnloadTimeUtc = DateTime.UtcNow.ToString("O")
            });
        }
    }
}
