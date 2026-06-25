#if !GODOT
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
            if (ResKit.GetProvider() == null)
            {
                LogKit.Warning("[YooInit] 请先初始化 YooAsset 并安装 ResKit Provider");
                return;
            }
            
            UIKit.SetPanelLoader(new YooAssetPanelLoaderPool());
        }
    }
}
#endif
#endif
