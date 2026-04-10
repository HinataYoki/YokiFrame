#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 标签页切换功能
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>
        /// 切换到 YooAsset 资源收集标签页
        /// </summary>
        public void SwitchToYooAssetCollector() => mTabView?.SwitchTo(1);

        /// <summary>
        /// 切换到资源监控标签页
        /// </summary>
        public void SwitchToResourceMonitor() => mTabView?.SwitchTo(0);
    }
}
#endif
