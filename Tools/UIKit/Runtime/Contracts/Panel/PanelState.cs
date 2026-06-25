#if !GODOT
namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板生命周期状态。
    /// </summary>
    public enum PanelState
    {
        /// <summary>
        /// 面板已打开。
        /// </summary>
        Open,

        /// <summary>
        /// 面板已隐藏。
        /// </summary>
        Hide,

        /// <summary>
        /// 面板已关闭。
        /// </summary>
        Close,
    }
}
#endif
