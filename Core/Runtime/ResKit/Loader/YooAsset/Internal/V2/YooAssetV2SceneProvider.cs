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
            // 2.3.x 静态 API 第 4 参是 suspendLoad（true=挂起），与 V3 的 allowSceneActivation 语义相反，
            // 故此处取反：activateOnLoad=true（想激活）等价于 suspendLoad=false。
            mHandle = YooAssets.LoadSceneAsync(path, mode, LocalPhysicsMode.None, suspendLoad: !activateOnLoad);
            return mHandle;
        }

        public AsyncOperationBase UnloadSceneAsync(YooAsset.SceneHandle handle)
            => handle.UnloadAsync();

        public void ResumeSuspendedScene(YooAsset.SceneHandle handle)
            => handle.UnSuspend();

        public void ReleaseHandle()
        {
            mHandle = default;
        }
    }
}
#endif
