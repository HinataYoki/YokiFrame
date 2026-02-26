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
            Root?.SetDefaultDialogType<T>();
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            Root?.SetDefaultPromptType<T>();
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            Root?.ShowDialog(config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null)
            where T : UIDialogPanel
        {
            Root?.ShowDialog<T>(config, onResult);
        }

        /// <summary>
        /// Alert 对话框
        /// </summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            Root?.Alert(message, title, onClose);
        }

        /// <summary>
        /// Confirm 对话框
        /// </summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            Root?.Confirm(message, title, onResult);
        }

        /// <summary>
        /// Prompt 对话框
        /// </summary>
        public static void Prompt(string message, string title = null, string defaultValue = null,
            Action<bool, string> onResult = null)
        {
            Root?.Prompt(message, title, defaultValue, onResult);
        }

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public static bool HasActiveDialog => Root?.HasActiveDialog ?? false;

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public static void ClearDialogQueue() => Root?.ClearDialogQueue();

        #endregion
    }
}
