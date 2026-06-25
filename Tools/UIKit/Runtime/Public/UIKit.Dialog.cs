#if !GODOT
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

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
        /// 异步显示对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<DialogResultData> ShowDialogAsync(DialogConfig config, CancellationToken ct = default)
#else
        public static Task<DialogResultData> ShowDialogAsync(DialogConfig config, CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.ShowDialogAsync(config, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(new DialogResultData { Result = DialogResult.Cancel });
#else
            return Task.FromResult(new DialogResultData { Result = DialogResult.Cancel });
#endif
        }

        /// <summary>
        /// 异步显示指定类型的对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<DialogResultData> ShowDialogAsync<T>(DialogConfig config, CancellationToken ct = default)
            where T : UIDialogPanel
#else
        public static Task<DialogResultData> ShowDialogAsync<T>(DialogConfig config, CancellationToken ct = default)
            where T : UIDialogPanel
#endif
        {
            var root = Root;
            if (root != default)
                return root.ShowDialogAsync<T>(config, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(new DialogResultData { Result = DialogResult.Cancel });
#else
            return Task.FromResult(new DialogResultData { Result = DialogResult.Cancel });
#endif
        }

        /// <summary>
        /// 异步显示指定类型的对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<DialogResultData> ShowDialogAsync(Type panelType, DialogConfig config, CancellationToken ct = default)
#else
        public static Task<DialogResultData> ShowDialogAsync(Type panelType, DialogConfig config, CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.ShowDialogAsync(panelType, config, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(new DialogResultData { Result = DialogResult.Cancel });
#else
            return Task.FromResult(new DialogResultData { Result = DialogResult.Cancel });
#endif
        }

        /// <summary>
        /// 异步 Alert 对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask AlertAsync(string message, string title = null, CancellationToken ct = default)
#else
        public static Task AlertAsync(string message, string title = null, CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.AlertAsync(message, title, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.CompletedTask;
#else
            return Task.CompletedTask;
#endif
        }

        /// <summary>
        /// 异步 Confirm 对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> ConfirmAsync(string message, string title = null, CancellationToken ct = default)
#else
        public static Task<bool> ConfirmAsync(string message, string title = null, CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.ConfirmAsync(message, title, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(false);
#else
            return Task.FromResult(false);
#endif
        }

        /// <summary>
        /// 异步 Prompt 对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<(bool confirmed, string value)> PromptAsync(
            string message, string title = null, string defaultValue = null, CancellationToken ct = default)
#else
        public static Task<(bool confirmed, string value)> PromptAsync(
            string message, string title = null, string defaultValue = null, CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.PromptAsync(message, title, defaultValue, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult((false, defaultValue));
#else
            return Task.FromResult((false, defaultValue));
#endif
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
        /// 当前等待中的对话框数量。
        /// </summary>
        public static int DialogQueueCount
        {
            get
            {
                var root = Root;
                return root != default ? root.DialogQueueCount : 0;
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
#endif
