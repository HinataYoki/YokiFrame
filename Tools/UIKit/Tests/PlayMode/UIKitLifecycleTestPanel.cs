#if UNITY_2022_3_OR_NEWER
namespace YokiFrame.Tests
{
    /// <summary>
    /// 记录 UIKit 面板生命周期次数，供 EditMode 与 PlayMode 公开 API 测试复用。
    /// </summary>
    public sealed class UIKitLifecycleTestPanel : UIPanel
    {
        public int InitCount { get; private set; }
        public int OpenCount { get; private set; }
        public int WillShowCount { get; private set; }
        public int ShowCount { get; private set; }
        public int DidShowCount { get; private set; }
        public int WillHideCount { get; private set; }
        public int HideCount { get; private set; }
        public int DidHideCount { get; private set; }
        public int CloseCount { get; private set; }
        public int BeforeDestroyCount { get; private set; }
        public IUIData LastInitData { get; private set; }
        public IUIData LastOpenData { get; private set; }

        /// <summary>
        /// 记录实例首次物化以及首个初始化数据。
        /// </summary>
        protected override void OnInit(IUIData data = null)
        {
            InitCount++;
            LastInitData = data;
        }

        /// <summary>
        /// 记录每一次显式打开以及当前轮次数据。
        /// </summary>
        protected override void OnOpen(IUIData data = null)
        {
            OpenCount++;
            LastOpenData = data;
        }

        /// <summary>
        /// 记录面板进入显示转换前的通知。
        /// </summary>
        protected override void OnWillShow()
        {
            WillShowCount++;
        }

        /// <summary>
        /// 记录面板提交可见状态的通知。
        /// </summary>
        protected override void OnShow()
        {
            ShowCount++;
        }

        /// <summary>
        /// 记录面板完成显示转换的通知。
        /// </summary>
        protected override void OnDidShow()
        {
            DidShowCount++;
        }

        /// <summary>
        /// 记录面板进入隐藏转换前的通知。
        /// </summary>
        protected override void OnWillHide()
        {
            WillHideCount++;
        }

        /// <summary>
        /// 记录面板提交隐藏状态的通知。
        /// </summary>
        protected override void OnHide()
        {
            HideCount++;
        }

        /// <summary>
        /// 记录面板完成隐藏转换的通知。
        /// </summary>
        protected override void OnDidHide()
        {
            DidHideCount++;
        }

        /// <summary>
        /// 记录当前打开轮次关闭。
        /// </summary>
        protected override void OnClose()
        {
            CloseCount++;
        }

        /// <summary>
        /// 记录实例资源即将被 UIKit 释放。
        /// </summary>
        protected override void OnBeforeDestroy()
        {
            BeforeDestroyCount++;
        }
    }
}
#endif
