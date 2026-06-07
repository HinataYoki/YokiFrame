#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER
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
    /// YooInit 资源加载功能 — 2.x 版本
    /// 2.x 使用 YooAssets 静态 API
    /// </summary>
    public static partial class YooInit
    {
        #region 资源信息

        /// <summary>
        /// 根据 Tag 获取资源信息
        /// </summary>
        public static AssetInfo[] GetAssetInfosByTag(string tag)
        {
            return YooAssets.GetAssetInfos(tag) ?? Array.Empty<AssetInfo>();
        }

        #endregion

        #region 原始文件加载

        /// <summary>
        /// 同步加载原始文件数据
        /// </summary>
        public static byte[] LoadRawFileData(string path)
        {
            var handle = YooAssets.LoadRawFileSync(path);
            var data = handle.GetRawFileData();
            handle.Release();
            return data;
        }

        /// <summary>
        /// 同步加载原始文件文本
        /// </summary>
        public static string LoadRawFileText(string path)
        {
            var handle = YooAssets.LoadRawFileSync(path);
            var text = handle.GetRawFileText();
            handle.Release();
            return text;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载原始文件
        /// </summary>
        public static async UniTask<RawFileOperationHandle> LoadRawAsync(string path, CancellationToken ct = default)
        {
            var handle = YooAssets.LoadRawFileAsync(path);
            await handle.ToUniTask(cancellationToken: ct);
            return handle;
        }
#else
        /// <summary>
        /// 异步加载原始文件
        /// </summary>
        public static IEnumerator LoadRawAsync(string path, Action<RawFileOperationHandle> onComplete)
        {
            var handle = YooAssets.LoadRawFileAsync(path);
            yield return handle;
            onComplete?.Invoke(handle);
        }
#endif

        #endregion

        #region 资源管理

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 卸载未使用资源
        /// </summary>
        public static async UniTask UnloadUnusedAssetsAsync()
        {
            var op = YooAssets.UnloadUnusedAssets();
            await op.ToUniTask();
        }
#else
        /// <summary>
        /// 卸载未使用资源
        /// </summary>
        public static IEnumerator UnloadUnusedAssetsAsync()
        {
            yield return YooAssets.UnloadUnusedAssets();
        }
#endif

        #endregion
    }
}
#endif
