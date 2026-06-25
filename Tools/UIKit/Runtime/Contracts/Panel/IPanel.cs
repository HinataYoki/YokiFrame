#if !GODOT
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板实例接口。
    /// </summary>
    public interface IPanel
    {
        /// <summary>
        /// 面板 Transform。
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// 面板处理句柄。
        /// </summary>
        PanelHandler Handler { get; set; }

        /// <summary>
        /// 面板名称。
        /// </summary>
        string PanelName { get; }

        /// <summary>
        /// 面板显示层级。
        /// </summary>
        UILevel Level { get; set; }

        /// <summary>
        /// 面板标签。
        /// </summary>
        string Tag { get; set; }

        /// <summary>
        /// 面板数据。
        /// </summary>
        IUIData Data { get; set; }

        /// <summary>
        /// 面板状态
        /// </summary>
        PanelState State { get; set; }

        /// <summary>
        /// 初始化面板。
        /// </summary>
        /// <param name="data">面板初始化数据。</param>
        void Init(IUIData data = null);

        /// <summary>
        /// 打开面板。
        /// </summary>
        /// <param name="data">面板打开数据。</param>
        void Open(IUIData data = null);

        /// <summary>
        /// 显示面板。
        /// </summary>
        void Show();

        /// <summary>
        /// 隐藏面板。
        /// </summary>
        void Hide();

        /// <summary>
        /// 关闭面板。
        /// </summary>
        void Close();
        
        /// <summary>
        /// 销毁前清理资源（由 UIKit 在 DestroyPanel 前调用）
        /// 用于处理 inactive GameObject 上 OnDestroy 不触发的问题
        /// </summary>
        void Cleanup();
    }
}
#endif
