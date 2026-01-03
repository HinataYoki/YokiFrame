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
    }
}