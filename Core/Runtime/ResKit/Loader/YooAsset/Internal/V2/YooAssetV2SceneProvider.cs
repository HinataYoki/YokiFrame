#if YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 场景提供者。
    /// 使用 YooAssets 静态 API。
    /// </summary>
    internal sealed class YooAssetV2SceneProvider : IYooAssetSceneProvider
    {
        private YooAsset.SceneHandle mHandle;

        public YooAsset.SceneHandle LoadSceneAsync(string path, LoadSceneMode mode, bool activateOnLoad)
        {
            mHandle = YooAssets.LoadSceneAsync(path, mode, LocalPhysicsMode.None, activateOnLoad);
            return mHandle;
        }

        public AsyncOperationBase UnloadSceneAsync(YooAsset.SceneHandle handle)
            => handle.UnloadAsync();

        public void ReleaseHandle()
        {
            mHandle = default;
        }
    }
}
#endif
