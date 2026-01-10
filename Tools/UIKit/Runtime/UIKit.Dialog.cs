using System;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIKit 对话框扩展
    /// </summary>
    public partial class UIKit
    {
        #region 对话框 API

        /// <summary>
        /// 设置默认对话框类型
        /// </summary>
        public static void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            UIDialogManager.SetDefaultDialogType<T>();
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            UIDialogManager.SetDefaultPromptType<T>();
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            UIDialogManager.ShowDialog(config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null) where T : UIDialogPanel
        {
            UIDialogManager.ShowDialog<T>(config, onResult);
        }

        /// <summary>
        /// 显示 Alert 对话框
        /// </summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            UIDialogManager.Alert(message, title, onClose);
        }

        /// <summary>
        /// 显示 Confirm 对话框
        /// </summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            UIDialogManager.Confirm(message, title, onResult);
        }

        /// <summary>
        /// 显示 Prompt 对话框
        /// </summary>
        public static void Prompt(string message, string title = null, string defaultValue = null, Action<bool, string> onResult = null)
        {
            UIDialogManager.Prompt(message, title, defaultValue, onResult);
        }

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public static bool HasActiveDialog => UIDialogManager.HasActiveDialog;

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public static void ClearDialogQueue()
        {
            UIDialogManager.ClearQueue();
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 对话框

        /// <summary>
        /// [UniTask] 显示对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync(DialogConfig config, CancellationToken ct = default)
        {
            return UIDialogManager.ShowDialogUniTaskAsync(config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync<T>(DialogConfig config, CancellationToken ct = default) where T : UIDialogPanel
        {
            return UIDialogManager.ShowDialogUniTaskAsync<T>(config, ct);
        }

        /// <summary>
        /// [UniTask] Alert 对话框
        /// </summary>
        public static UniTask AlertUniTaskAsync(string message, string title = null, CancellationToken ct = default)
        {
            return UIDialogManager.AlertUniTaskAsync(message, title, ct);
        }

        /// <summary>
        /// [UniTask] Confirm 对话框
        /// </summary>
        public static UniTask<bool> ConfirmUniTaskAsync(string message, string title = null, CancellationToken ct = default)
        {
            return UIDialogManager.ConfirmUniTaskAsync(message, title, ct);
        }

        /// <summary>
        /// [UniTask] Prompt 对话框
        /// </summary>
        public static UniTask<(bool confirmed, string value)> PromptUniTaskAsync(string message, string title = null, string defaultValue = null, CancellationToken ct = default)
        {
            return UIDialogManager.PromptUniTaskAsync(message, title, defaultValue, ct);
        }

        #endregion
#endif
    }
}
