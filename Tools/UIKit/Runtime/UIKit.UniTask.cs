#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// UIKit UniTask 扩展
    /// </summary>
    public partial class UIKit
    {
        #region 面板 UniTask

        /// <summary>
        /// [UniTask] 异步打开面板
        /// </summary>
        public static async UniTask<T> OpenPanelUniTaskAsync<T>(UILevel level = UILevel.Common,
            IUIData data = null, CancellationToken ct = default) where T : UIPanel
        {
            var tcs = new UniTaskCompletionSource<IPanel>();
            ct.Register(() => tcs.TrySetCanceled());
            UIRoot.Instance.OpenPanelAsyncInternal(typeof(T), level, data, panel => tcs.TrySetResult(panel));
            var result = await tcs.Task;
            return result as T;
        }

        /// <summary>
        /// [UniTask] 异步打开面板（通过 Type）
        /// </summary>
        public static async UniTask<IPanel> OpenPanelUniTaskAsync(Type type, UILevel level = UILevel.Common,
            IUIData data = null, CancellationToken ct = default)
        {
            var tcs = new UniTaskCompletionSource<IPanel>();
            ct.Register(() => tcs.TrySetCanceled());
            UIRoot.Instance.OpenPanelAsyncInternal(type, level, data, panel => tcs.TrySetResult(panel));
            return await tcs.Task;
        }

        /// <summary>
        /// [UniTask] 异步打开并压入面板
        /// </summary>
        public static async UniTask<T> PushOpenPanelUniTaskAsync<T>(UILevel level = UILevel.Common,
            IUIData data = null, bool hidePreLevel = true, CancellationToken ct = default) where T : UIPanel
        {
            var panel = await OpenPanelUniTaskAsync<T>(level, data, ct);
            if (panel != default)
            {
                UIRoot.Instance.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
            }
            return panel;
        }

        /// <summary>
        /// [UniTask] 异步弹出面板
        /// </summary>
        public static UniTask<IPanel> PopPanelUniTaskAsync(string stackName = UIRoot.DEFAULT_STACK,
            bool showPrevious = true, bool autoClose = true, CancellationToken ct = default)
        {
            return UIRoot.Instance.PopFromStackUniTaskAsync(stackName, showPrevious, autoClose, ct);
        }

        #endregion

        #region 缓存 UniTask

        /// <summary>
        /// [UniTask] 预加载面板
        /// </summary>
        public static UniTask<bool> PreloadPanelUniTaskAsync<T>(UILevel level = UILevel.Common,
            CancellationToken ct = default) where T : UIPanel
        {
            return UIRoot.Instance.PreloadPanelUniTaskAsync<T>(level, ct);
        }

        /// <summary>
        /// [UniTask] 预加载面板
        /// </summary>
        public static UniTask<bool> PreloadPanelUniTaskAsync(Type panelType, UILevel level = UILevel.Common,
            CancellationToken ct = default)
        {
            return UIRoot.Instance.PreloadPanelUniTaskAsync(panelType, level, ct);
        }

        #endregion

        #region 对话框 UniTask

        /// <summary>
        /// [UniTask] 显示对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync(DialogConfig config,
            CancellationToken ct = default)
        {
            return UIRoot.Instance.ShowDialogUniTaskAsync(config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync<T>(DialogConfig config,
            CancellationToken ct = default) where T : UIDialogPanel
        {
            return UIRoot.Instance.ShowDialogUniTaskAsync<T>(config, ct);
        }

        /// <summary>
        /// [UniTask] Alert 对话框
        /// </summary>
        public static UniTask AlertUniTaskAsync(string message, string title = null,
            CancellationToken ct = default)
        {
            return UIRoot.Instance.AlertUniTaskAsync(message, title, ct);
        }

        /// <summary>
        /// [UniTask] Confirm 对话框
        /// </summary>
        public static UniTask<bool> ConfirmUniTaskAsync(string message, string title = null,
            CancellationToken ct = default)
        {
            return UIRoot.Instance.ConfirmUniTaskAsync(message, title, ct);
        }

        /// <summary>
        /// [UniTask] Prompt 对话框
        /// </summary>
        public static UniTask<(bool confirmed, string value)> PromptUniTaskAsync(string message,
            string title = null, string defaultValue = null, CancellationToken ct = default)
        {
            return UIRoot.Instance.PromptUniTaskAsync(message, title, defaultValue, ct);
        }

        #endregion
    }
}
#endif
