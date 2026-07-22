#if UNITY_2022_3_OR_NEWER
namespace YokiFrame
{
    /// <summary>
    /// 表示已实例化 UIKit 面板的公开生命周期状态。
    /// </summary>
    public enum PanelState
    {
        /// <summary>面板已完成实例化和初始化，但尚未打开。</summary>
        Preloaded,

        /// <summary>面板正在进入本次打开或重新显示流程。</summary>
        Opening,

        /// <summary>面板已打开并可见。</summary>
        Open,

        /// <summary>面板正在执行隐藏流程。</summary>
        Hiding,

        /// <summary>面板已隐藏，但当前打开轮次仍然有效。</summary>
        Hide,

        /// <summary>面板正在执行关闭流程。</summary>
        Closing,

        /// <summary>面板已关闭并保留在复用缓存中。</summary>
        Cached,

        /// <summary>面板已关闭，且不再属于可操作的打开轮次。</summary>
        Close
    }
}
#endif
