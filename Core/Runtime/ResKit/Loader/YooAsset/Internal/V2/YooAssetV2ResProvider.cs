#if YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 资源提供者。
    /// 使用 YooAssets 静态 API。
    /// 文件内零条件编译 — 纯 V2 代码。
    /// </summary>
    internal sealed class YooAssetV2ResProvider : IYooAssetResProvider
#if YOKIFRAME_UNITASK_SUPPORT
        , IYooAssetResUniTaskProvider
#endif
    {
        private AssetHandle mHandle;
        private AllAssetsHandle mAllHandle;
        private SubAssetsHandle mSubHandle;

        // ──────────── Sync ────────────

        public T LoadAsset<T>(string path) where T : Object
        {
            mHandle = YooAssets.LoadAssetSync<T>(path);
            return mHandle.AssetObject as T;
        }

        public T[] LoadAllAssets<T>(string path) where T : Object
        {
            mAllHandle = YooAssets.LoadAllAssetsSync<T>(path);
            if (mAllHandle.Status != EOperationStatus.Succeed)
                return Array.Empty<T>();
            return ConvertAll<T>(mAllHandle.AllAssetObjects);
        }

        public SubAssetsResult<T> LoadSubAssets<T>(string path) where T : Object
        {
            mSubHandle = YooAssets.LoadSubAssetsSync<T>(path);
            if (mSubHandle.Status != EOperationStatus.Succeed)
                return default;
            return ConvertSub<T>(mSubHandle.SubAssetObjects);
        }

        // ──────────── Async Callback ────────────

        public void LoadAssetAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            mHandle = YooAssets.LoadAssetAsync<T>(path);
            mHandle.Completed += h => onComplete?.Invoke(h.AssetObject as T);
        }

        public void LoadAllAssetsAsync<T>(string path, Action<T[]> onComplete) where T : Object
        {
            mAllHandle = YooAssets.LoadAllAssetsAsync<T>(path);
            mAllHandle.Completed += h =>
            {
                if (h.Status != EOperationStatus.Succeed) { onComplete?.Invoke(Array.Empty<T>()); return; }
                onComplete?.Invoke(ConvertAll<T>(h.AllAssetObjects));
            };
        }

        public void LoadSubAssetsAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object
        {
            mSubHandle = YooAssets.LoadSubAssetsAsync<T>(path);
            mSubHandle.Completed += h =>
            {
                if (h.Status != EOperationStatus.Succeed) { onComplete?.Invoke(default); return; }
                onComplete?.Invoke(ConvertSub<T>(h.SubAssetObjects));
            };
        }

        // ──────────── Cleanup ────────────

        public void ReleaseHandles()
        {
            mHandle?.Release();
            mAllHandle?.Release();
            mSubHandle?.Release();
            mHandle = null;
            mAllHandle = null;
            mSubHandle = null;
        }

        // ──────────── Converters ────────────

        private static T[] ConvertAll<T>(System.Collections.Generic.IReadOnlyList<Object> objects) where T : Object
        {
            var result = new T[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                result[i] = objects[i] as T;
            return result;
        }

        private static SubAssetsResult<T> ConvertSub<T>(System.Collections.Generic.IReadOnlyList<Object> objects) where T : Object
        {
            T main = null;
            var subList = new System.Collections.Generic.List<T>(objects.Count);
            foreach (var obj in objects)
            {
                if (obj is T typed) { main ??= typed; subList.Add(typed); }
            }
            return new SubAssetsResult<T>(main, subList.ToArray());
        }

#if YOKIFRAME_UNITASK_SUPPORT
        // ──────────── UniTask ────────────

        public async UniTask<T> LoadAssetUniTaskAsync<T>(string path, CancellationToken ct) where T : Object
        {
            mHandle = YooAssets.LoadAssetAsync<T>(path);
            await mHandle.ToUniTask(cancellationToken: ct);
            return mHandle.AssetObject as T;
        }

        public async UniTask<T[]> LoadAllAssetsUniTaskAsync<T>(string path, CancellationToken ct) where T : Object
        {
            mAllHandle = YooAssets.LoadAllAssetsAsync<T>(path);
            await mAllHandle.ToUniTask(cancellationToken: ct);
            if (mAllHandle.Status != EOperationStatus.Succeed)
                return Array.Empty<T>();
            return ConvertAll<T>(mAllHandle.AllAssetObjects);
        }

        public async UniTask<SubAssetsResult<T>> LoadSubAssetsUniTaskAsync<T>(string path, CancellationToken ct) where T : Object
        {
            mSubHandle = YooAssets.LoadSubAssetsAsync<T>(path);
            await mSubHandle.ToUniTask(cancellationToken: ct);
            if (mSubHandle.Status != EOperationStatus.Succeed)
                return default;
            return ConvertSub<T>(mSubHandle.SubAssetObjects);
        }
#endif
    }
}
#endif
