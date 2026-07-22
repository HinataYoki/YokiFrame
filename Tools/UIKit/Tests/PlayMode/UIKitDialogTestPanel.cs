#if UNITY_2022_3_OR_NEWER
namespace YokiFrame.Tests
{
    /// <summary>Dialog 队列测试使用的最小具体对话框面板。</summary>
    public sealed class UIKitDialogTestPanel : UIDialogPanel
    {
        /// <summary>记录当前测试进程中的内容绑定次数。</summary>
        public static int SetupCount { get; private set; }

        /// <summary>记录最近一次配置。</summary>
        public static DialogConfig LastConfig { get; private set; }

        /// <summary>重置测试计数。</summary>
        public static void ResetCounters()
        {
            SetupCount = 0;
            LastConfig = null;
        }

        /// <summary>模拟用户提交结果并关闭对话框。</summary>
        public void Complete(DialogResult result, string inputValue = null)
        {
            SendResult(result, inputValue);
            CloseSelf();
        }

        /// <summary>记录 UIKit 传入的 Dialog 配置。</summary>
        protected override void SetupDialog(DialogConfig config)
        {
            SetupCount++;
            LastConfig = config;
        }
    }
}
#endif
