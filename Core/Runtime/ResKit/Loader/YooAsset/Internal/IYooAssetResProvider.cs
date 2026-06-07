#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载能力抽象。
    /// 隔离 V2（静态 API）与 V3（ResourcePackage API）的差异，
    /// 使 Loader 成为版本无关的单一实现。
    /// 本接口不含版本宏 — 每个 YooAsset 版本提供独立的实现。
    /// </summary>
    internal interface IYooAssetResProvider
    {
        T LoadAsset<T>(string path) where T : Object;
        void LoadAssetAsync<T>(string path, Action<T> onComplete) where T : Object;

        T[] LoadAllAssets<T>(string path) where T : Object;
        void LoadAllAssetsAsync<T>(string path, Action<T[]> onComplete) where T : Object;

        SubAssetsResult<T> LoadSubAssets<T>(string path) where T : Object;
        void LoadSubAssetsAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object;

        void ReleaseHandles();
    }
}
#endif
