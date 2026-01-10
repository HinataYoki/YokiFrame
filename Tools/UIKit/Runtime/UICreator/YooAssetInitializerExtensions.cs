#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
namespace YokiFrame
{
    /// <summary>
    /// YooInit 的 UIKit 扩展
    /// </summary>
    public static class YooInitUIKitExt
    {
        /// <summary>
        /// 配置 UIKit 使用 YooAsset 加载面板
        /// 在 YooInit.InitAsync() 之后调用
        /// </summary>
        public static void ConfigureUIKit()
        {
            if (!YooInit.Initialized)
            {
                KitLogger.Warning("[YooInit] 请先调用 InitAsync() 初始化 YooAsset");
                return;
            }
            
            UIKit.SetPanelLoader(new YooAssetPanelLoaderPool());
            KitLogger.Log("[YooInit] UIKit 面板加载器已配置为 YooAsset");
        }
    }
}
#endif
