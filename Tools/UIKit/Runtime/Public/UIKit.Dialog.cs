using System;

namespace YokiFrame
{
    /// <summary>
    /// UI 管理工具 - 对话框
    /// </summary>
    public partial class UIKit
    {
        #region 对话框

        /// <summary>
        /// 设置默认对话框类型
        /// </summary>
        public static void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            var root = Root;
            if (root == default) return;
            root.SetDefaultDialogType<T>();
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            var root = Root;
            if (root == default) return;
            root.SetDefaultPromptType<T>();
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            var root = Root;
            if (root == default) return;
            root.ShowDialog(config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null)
            where T : UIDialogPanel
        {
            var root = Root;
            if (root == default) return;
            root.ShowDialog<T>(config, onResult);
        }

        /// <summary>
        /// Alert 对话框
        /// </summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            var root = Root;
            if (root == default) return;
            root.Alert(message, title, onClose);
        }

        /// <summary>
        /// Confirm 对话框
        /// </summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            var root = Root;
            if (root == default) return;
            root.Confirm(message, title, onResult);
        }

        /// <summary>
        /// Prompt 对话框
        /// </summary>
        public static void Prompt(string message, string title = null, string defaultValue = null,
            Action<bool, string> onResult = null)
        {
            var root = Root;
            if (root == default) return;
            root.Prompt(message, title, defaultValue, onResult);
        }

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public static bool HasActiveDialog
        {
            get
            {
                var root = Root;
                return root != default ? root.HasActiveDialog : false;
            }
        }

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public static void ClearDialogQueue()
        {
            var root = Root;
            if (root != default) root.ClearDialogQueue();
        }

        #endregion
    }
}
