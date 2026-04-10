using UnityEngine;

namespace YokiFrame
{
    public interface IPanel
    {
        Transform Transform { get; }
        PanelHandler Handler { get; set; }
        /// <summary>
        /// 面板状态
        /// </summary>
        PanelState State { get; set; }



        void Init(IUIData data = null);
        void Open(IUIData data = null);
        void Show();
        void Hide();
        void Close();
        
        /// <summary>
        /// 销毁前清理资源（由 UIKit 在 DestroyPanel 前调用）
        /// 用于处理 inactive GameObject 上 OnDestroy 不触发的问题
        /// </summary>
        void Cleanup();
    }
}