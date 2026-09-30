#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>集中判断 YooAsset package 是否还能安全参与加载和 location 探测。</summary>
    internal static class YooAssetPackageReadiness
    {
        /// <summary>确认全局驱动、package 初始化和 manifest 都处于可用状态。</summary>
        /// <param name="package">待检查的 package。</param>
        /// <returns>可以执行 <c>IsLocationValid</c> 和加载时返回 true。</returns>
        public static bool IsReady(ResourcePackage package)
        {
            if (package == null)
                return false;

#if YOKIFRAME_YOOASSET_3
            bool initialized = YooAssets.IsInitialized;
            bool succeeded = package.InitializeStatus == EOperationStatus.Succeeded;
#else
            bool initialized = YooAssets.Initialized;
            bool succeeded = package.InitializeStatus == EOperationStatus.Succeed;
#endif
            return initialized && succeeded && package.PackageValid;
        }
    }
}
#endif
