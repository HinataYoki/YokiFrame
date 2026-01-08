using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 对话框队列项
    /// </summary>
    internal class DialogQueueItem
    {
        public Type PanelType;
        public DialogConfig Config;
        public Action<DialogResultData> OnResult;
        public UILevel Level;
    }

    /// <summary>
    /// UI 对话框管理器 - 管理对话框队列和显示
    /// </summary>
    internal static class UIDialogManager
    {
        #region 配置

        /// <summary>
        /// 默认对话框面板类型
        /// </summary>
        private static Type sDefaultDialogType;

        /// <summary>
        /// 默认输入对话框面板类型
        /// </summary>
        private static Type sDefaultPromptType;

        /// <summary>
        /// 对话框显示层级
        /// </summary>
        private static UILevel sDialogLevel = UILevel.Pop;

        #endregion

        #region 状态

        private static readonly Queue<DialogQueueItem> sDialogQueue = new();
        private static UIDialogPanel sCurrentDialog;
        private static bool sIsProcessing;

        #endregion

        #region 配置方法

        /// <summary>
        /// 设置默认对话框类型
        /// </summary>
        public static void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            sDefaultDialogType = typeof(T);
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            sDefaultPromptType = typeof(T);
        }

        /// <summary>
        /// 设置对话框显示层级
        /// </summary>
        public static void SetDialogLevel(UILevel level)
        {
            sDialogLevel = level;
        }

        #endregion

        #region 显示方法

        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            ShowDialog(sDefaultDialogType, config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null) where T : UIDialogPanel
        {
            ShowDialog(typeof(T), config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog(Type panelType, DialogConfig config, Action<DialogResultData> onResult = null)
        {
            if (panelType == null)
            {
                KitLogger.Error("[UIDialogManager] 对话框类型未设置，请先调用 SetDefaultDialogType");
                onResult?.Invoke(new DialogResultData { Result = DialogResult.Cancel });
                return;
            }

            var item = new DialogQueueItem
            {
                PanelType = panelType,
                Config = config,
                OnResult = onResult,
                Level = sDialogLevel
            };

            sDialogQueue.Enqueue(item);
            ProcessQueue();
        }

        /// <summary>
        /// 显示 Alert 对话框
        /// </summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            var config = DialogConfig.Alert(message, title);
            ShowDialog(config, result => onClose?.Invoke());
        }

        /// <summary>
        /// 显示 Confirm 对话框
        /// </summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            var config = DialogConfig.Confirm(message, title);
            ShowDialog(config, result => onResult?.Invoke(result.IsConfirmed));
        }

        /// <summary>
        /// 显示 Prompt 对话框
        /// </summary>
        public static void Prompt(string message, string title = null, string defaultValue = null, Action<bool, string> onResult = null)
        {
            var config = PromptConfig.Create(message, title, defaultValue);
            var panelType = sDefaultPromptType ?? sDefaultDialogType;
            ShowDialog(panelType, config, result => onResult?.Invoke(result.IsConfirmed, result.InputValue));
        }

        #endregion

        #region 队列处理

        private static void ProcessQueue()
        {
            if (sIsProcessing || sDialogQueue.Count == 0) return;
            if (sCurrentDialog != null) return;

            sIsProcessing = true;
            var item = sDialogQueue.Dequeue();

            var data = new DialogData
            {
                Config = item.Config,
                OnResult = result =>
                {
                    item.OnResult?.Invoke(result);
                    OnDialogClosed();
                }
            };

            // 使用反射创建面板（因为类型是动态的）
            UIKit.OpenPanelAsync(item.PanelType, item.Level, data, panel =>
            {
                sCurrentDialog = panel as UIDialogPanel;
                if (sCurrentDialog == null)
                {
                    KitLogger.Error($"[UIDialogManager] 无法创建对话框: {item.PanelType.Name}");
                    data.OnResult?.Invoke(new DialogResultData { Result = DialogResult.Cancel });
                }
            });
        }

        private static void OnDialogClosed()
        {
            sCurrentDialog = null;
            sIsProcessing = false;
            ProcessQueue();
        }

        #endregion

        #region 查询

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public static bool HasActiveDialog => sCurrentDialog != null;

        /// <summary>
        /// 队列中的对话框数量
        /// </summary>
        public static int QueueCount => sDialogQueue.Count;

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public static void ClearQueue()
        {
            while (sDialogQueue.Count > 0)
            {
                var item = sDialogQueue.Dequeue();
                item.OnResult?.Invoke(new DialogResultData { Result = DialogResult.Cancel });
            }
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 方法

        /// <summary>
        /// [UniTask] 显示对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync(DialogConfig config, CancellationToken ct = default)
        {
            return ShowDialogUniTaskAsync(sDefaultDialogType, config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync<T>(DialogConfig config, CancellationToken ct = default) where T : UIDialogPanel
        {
            return ShowDialogUniTaskAsync(typeof(T), config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync(Type panelType, DialogConfig config, CancellationToken ct = default)
        {
            var tcs = new UniTaskCompletionSource<DialogResultData>();
            
            ct.Register(() => tcs.TrySetCanceled());

            ShowDialog(panelType, config, result =>
            {
                tcs.TrySetResult(result);
            });

            return tcs.Task;
        }

        /// <summary>
        /// [UniTask] Alert 对话框
        /// </summary>
        public static async UniTask AlertUniTaskAsync(string message, string title = null, CancellationToken ct = default)
        {
            var config = DialogConfig.Alert(message, title);
            await ShowDialogUniTaskAsync(config, ct);
        }

        /// <summary>
        /// [UniTask] Confirm 对话框
        /// </summary>
        public static async UniTask<bool> ConfirmUniTaskAsync(string message, string title = null, CancellationToken ct = default)
        {
            var config = DialogConfig.Confirm(message, title);
            var result = await ShowDialogUniTaskAsync(config, ct);
            return result.IsConfirmed;
        }

        /// <summary>
        /// [UniTask] Prompt 对话框
        /// </summary>
        public static async UniTask<(bool confirmed, string value)> PromptUniTaskAsync(string message, string title = null, string defaultValue = null, CancellationToken ct = default)
        {
            var config = PromptConfig.Create(message, title, defaultValue);
            var panelType = sDefaultPromptType ?? sDefaultDialogType;
            var result = await ShowDialogUniTaskAsync(panelType, config, ct);
            return (result.IsConfirmed, result.InputValue);
        }

        #endregion
#endif
    }
}
