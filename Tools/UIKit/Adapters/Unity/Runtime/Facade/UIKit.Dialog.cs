#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    public static partial class UIKit
    {
        /// <summary>设置默认 Dialog 面板类型。</summary>
        public static void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            RequireController().SetDefaultDialogType(typeof(T));
        }

        /// <summary>设置默认 Prompt 面板类型。</summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            RequireController().SetDefaultPromptType(typeof(T));
        }

        /// <summary>显示默认类型的 Dialog。</summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            UIKitController controller = RequireController();
            controller.ShowDialog(controller.DefaultDialogType, config, onResult);
        }

        /// <summary>显示指定类型的 Dialog。</summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null)
            where T : UIDialogPanel
        {
            RequireController().ShowDialog(typeof(T), config, onResult);
        }

        /// <summary>通过运行时 Type 显示指定 UIDialogPanel。</summary>
        public static void ShowDialog(
            Type panelType,
            DialogConfig config,
            Action<DialogResultData> onResult = null)
        {
            RequireController().ShowDialog(panelType, config, onResult);
        }

        /// <summary>显示 Alert 对话框。</summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            ShowDialog(DialogConfig.Alert(message, title), result =>
            {
                if (onClose != null) onClose();
            });
        }

        /// <summary>显示 Confirm 对话框。</summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            ShowDialog(DialogConfig.Confirm(message, title), result =>
            {
                if (onResult != null) onResult(result.IsConfirmed);
            });
        }

        /// <summary>显示 Prompt 对话框。</summary>
        public static void Prompt(string message, string title = null, string defaultValue = null, Action<bool, string> onResult = null)
        {
            UIKitController controller = RequireController();
            Type panelType = controller.DefaultPromptType ?? controller.DefaultDialogType;
            controller.ShowDialog(panelType, PromptConfig.Create(message, title, defaultValue), result =>
            {
                if (onResult != null) onResult(result.IsConfirmed, result.InputValue);
            });
        }

        /// <summary>异步显示默认类型 Dialog，取消只影响当前调用方。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<DialogResultData> ShowDialogAsync(
#else
        public static async Task<DialogResultData> ShowDialogAsync(
#endif
            DialogConfig config,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            UIKitController controller = RequireController();
            return await controller.ShowDialogAsync(controller.DefaultDialogType, config, token);
        }

        /// <summary>异步显示指定类型 Dialog。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<DialogResultData> ShowDialogAsync<T>(
#else
        public static async Task<DialogResultData> ShowDialogAsync<T>(
#endif
            DialogConfig config,
            CancellationToken token = default)
            where T : UIDialogPanel
        {
            token.ThrowIfCancellationRequested();
            return await RequireController().ShowDialogAsync(typeof(T), config, token);
        }


        /// <summary>通过运行时 Type 异步显示指定 UIDialogPanel。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<DialogResultData> ShowDialogAsync(
#else
        public static async Task<DialogResultData> ShowDialogAsync(
#endif
            Type panelType,
            DialogConfig config,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return await RequireController().ShowDialogAsync(panelType, config, token);
        }

        /// <summary>异步显示 Alert。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask AlertAsync(
#else
        public static async Task AlertAsync(
#endif
            string message,
            string title = null,
            CancellationToken token = default)
        {
            await ShowDialogAsync(DialogConfig.Alert(message, title), token);
        }

        /// <summary>异步显示 Confirm 并返回是否确认。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<bool> ConfirmAsync(
#else
        public static async Task<bool> ConfirmAsync(
#endif
            string message,
            string title = null,
            CancellationToken token = default)
        {
            DialogResultData result = await ShowDialogAsync(DialogConfig.Confirm(message, title), token);
            return result.IsConfirmed;
        }

        /// <summary>异步显示 Prompt 并返回确认状态和输入值。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<(bool confirmed, string value)> PromptAsync(
#else
        public static async Task<(bool confirmed, string value)> PromptAsync(
#endif
            string message,
            string title = null,
            string defaultValue = null,
            CancellationToken token = default)
        {
            UIKitController controller = RequireController();
            Type panelType = controller.DefaultPromptType ?? controller.DefaultDialogType;
            DialogResultData result = await controller.ShowDialogAsync(panelType, PromptConfig.Create(message, title, defaultValue), token);
            return (result.IsConfirmed, result.InputValue);
        }

        /// <summary>读取是否存在活动 Dialog，不创建 UIRoot。</summary>
        public static bool HasActiveDialog
        {
            get
            {
                UIKitController controller = GetExistingController();
                return controller != null && controller.HasActiveDialog;
            }
        }

        /// <summary>读取等待中的 Dialog 数量，不创建 UIRoot。</summary>
        public static int DialogQueueCount
        {
            get
            {
                UIKitController controller = GetExistingController();
                return controller == null ? 0 : controller.DialogQueueCount;
            }
        }

        /// <summary>取消所有尚未显示的 Dialog。</summary>
        public static void ClearDialogQueue()
        {
            UIKitController controller = GetExistingController();
            if (controller != null) controller.ClearDialogQueue();
        }
    }
}
#endif
