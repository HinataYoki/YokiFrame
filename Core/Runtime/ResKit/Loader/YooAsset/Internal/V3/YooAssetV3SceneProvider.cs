#if YOOASSET_3_0_OR_NEWER
using System;
using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 3.x 场景提供者。
    /// 使用 ResourcePackage 实例 API。
    /// </summary>
    internal sealed class YooAssetV3SceneProvider : IYooAssetSceneProvider
    {
        private readonly ResourcePackage mPackage;
        private YooAsset.SceneHandle mHandle;

        public YooAssetV3SceneProvider(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        public YooAsset.SceneHandle LoadSceneAsync(string path, LoadSceneMode mode, bool activateOnLoad)
        {
            mHandle = mPackage.LoadSceneAsync(path, mode, LocalPhysicsMode.None, activateOnLoad);
            return mHandle;
        }

        public AsyncOperationBase UnloadSceneAsync(YooAsset.SceneHandle handle)
            => handle.UnloadSceneAsync();

        public void ReleaseHandle()
        {
            mHandle = default;
        }
    }
}
#endif
