#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#else
using System.Collections;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooInit 资源加载功能
    /// </summary>
    public static partial class YooInit
    {
        #region 资源信息

        /// <summary>
        /// 根据 Tag 获取资源信息
        /// </summary>
        public static AssetInfo[] GetAssetInfosByTag(string tag, ResourcePackage package = null)
        {
            if (package != null)
                return package.GetAssetInfos(tag) ?? Array.Empty<AssetInfo>();

            // 遍历所有包查找
            foreach (var pkg in sPackages.Values)
            {
                var infos = pkg.GetAssetInfos(tag);
                if (infos is { Length: > 0 })
                    return infos;
            }
            return Array.Empty<AssetInfo>();
        }

        #endregion

        #region 原始文件加载

        /// <summary>
        /// 同步加载原始文件数据（智能查找包）
        /// </summary>
        public static byte[] LoadRawFileData(string path, ResourcePackage package = null)
        {
            package ??= FindPackageForPath(path);
            if (package == default || !package.CheckLocationValid(path))
            {
                KitLogger.Error($"[YooInit] 无效路径: {path}");
                return null;
            }

            var handle = package.LoadRawFileSync(path);
            var data = handle.GetRawFileData();
            handle.Release();
            return data;
        }

        /// <summary>
        /// 同步加载原始文件文本（智能查找包）
        /// </summary>
        public static string LoadRawFileText(string path, ResourcePackage package = null)
        {
            package ??= FindPackageForPath(path);
            if (package == default || !package.CheckLocationValid(path))
            {
                KitLogger.Error($"[YooInit] 无效路径: {path}");
                return null;
            }

            var handle = package.LoadRawFileSync(path);
            var text = handle.GetRawFileText();
            handle.Release();
            return text;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载原始文件（智能查找包）
        /// </summary>
        public static async UniTask<RawFileHandle> LoadRawAsync(string path, ResourcePackage package = null, CancellationToken ct = default)
        {
            package ??= FindPackageForPath(path);
            if (package == null || !package.CheckLocationValid(path))
            {
                KitLogger.Error($"[YooInit] 无效路径: {path}");
                return default;
            }

            var handle = package.LoadRawFileAsync(path);
            await handle.ToUniTask(cancellationToken: ct);
            return handle;
        }
#else
        /// <summary>
        /// 异步加载原始文件（智能查找包）
        /// </summary>
        public static IEnumerator LoadRawAsync(string path, Action<RawFileHandle> onComplete, ResourcePackage package = null)
        {
            package ??= FindPackageForPath(path);
            if (package == default || !package.CheckLocationValid(path))
            {
                KitLogger.Error($"[YooInit] 无效路径: {path}");
                onComplete?.Invoke(default);
                yield break;
            }

            var handle = package.LoadRawFileAsync(path);
            yield return handle;
            onComplete?.Invoke(handle);
        }
#endif

        #endregion

        #region 资源管理

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 卸载未使用资源（所有包）
        /// </summary>
        public static async UniTask UnloadUnusedAssetsAsync()
        {
            foreach (var package in sPackages.Values)
            {
                await package.UnloadUnusedAssetsAsync().ToUniTask();
            }
        }
#else
        /// <summary>
        /// 卸载未使用资源（所有包）
        /// </summary>
        public static IEnumerator UnloadUnusedAssetsAsync()
        {
            foreach (var package in sPackages.Values)
            {
                yield return package.UnloadUnusedAssetsAsync();
            }
        }
#endif

        #endregion
    }
}
#endif
