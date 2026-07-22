#if UNITY_2022_3_OR_NEWER
namespace YokiFrame.Tests
{
    /// <summary>
    /// 记录命名栈焦点生命周期，供 Push、Pop 和直接 Close 恢复行为测试。
    /// </summary>
    public abstract class UIKitNavigationTestPanelBase : UIPanel
    {
        public int FocusCount { get; private set; }
        public int BlurCount { get; private set; }
        public int ResumeCount { get; private set; }
        public bool CloseOnBlur { get; set; }

        /// <summary>
        /// 记录面板成为当前命名栈顶部。
        /// </summary>
        protected override void OnFocus()
        {
            FocusCount++;
        }

        /// <summary>
        /// 记录面板离开当前命名栈顶部。
        /// </summary>
        protected override void OnBlur()
        {
            BlurCount++;
            if (CloseOnBlur) CloseSelf();
        }

        /// <summary>
        /// 记录上层面板离栈后的恢复通知。
        /// </summary>
        protected override void OnResume()
        {
            ResumeCount++;
        }
    }
}
#endif
