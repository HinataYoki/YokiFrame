using UnityEngine;

namespace YokiFrame
{
    public enum PanelState
    {
        Open,
        Hide,
        Close,
    }

    public enum UILevel
    {
        AlwayBottom,
        Bg,
        Common,
        Pop,
        AlwayTop,


        CanvasPanel,
    }

    public interface IPanel
    {
        Transform Transform { get; }
        PanelHandler Handler { get; set; }
        /// <summary>
        /// 面板状态
        /// </summary>
        PanelState State { get; set; }



        void Init(IUIData data = null);
        void Open();
        void Show();
        void Hide();
        void Close();
    }
}