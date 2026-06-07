using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 场景加载能力抽象。
    /// 隔离 V2（静态 API）与 V3（ResourcePackage API）差异。
    /// </summary>
    internal interface IYooAssetSceneProvider
    {
        YooAsset.SceneHandle LoadSceneAsync(string path, LoadSceneMode mode, bool activateOnLoad);
        AsyncOperationBase UnloadSceneAsync(YooAsset.SceneHandle handle);
        void ReleaseHandle();
    }
}
