using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 对话框子系统
    /// </summary>
    public partial class UIRoot
    {
        #region 对话框配置

        private Type mDefaultDialogType;
        private Type mDefaultPromptType;

        #endregion

        #region 对话框状态

        private readonly Queue<DialogQueueItem> mDialogQueue = new();
        private UIDialogPanel mCurrentDialog;
        private bool mIsDialogProcessing;

        #endregion

        #region 对话框配置方法

        /// <summary>
        /// 设置默认对话框类型
        /// </summary>
        public void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            mDefaultDialogType = typeof(T);
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            mDefaultPromptType = typeof(T);
        }

        #endregion

        #region 对话框显示

        /// <summary>
        /// 显示对话框
        /// </summary>
        public void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            ShowDialog(mDefaultDialogType, config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null)
            where T : UIDialogPanel
        {
            ShowDialog(typeof(T), config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public void ShowDialog(Type panelType, DialogConfig config, Action<DialogResultData> onResult)
        {
            if (panelType == default)
            {
                KitLogger.Error("[UIRoot] 对话框类型未设置，请先调用 SetDefaultDialogType");
                onResult?.Invoke(new DialogResultData { Result = DialogResult.Cancel });
                return;
            }

            var item = new DialogQueueItem
            {
                PanelType = panelType,
                Config = config,
                OnResult = onResult,
                Level = mConfig.DialogLevel
            };

            mDialogQueue.Enqueue(item);
            ProcessDialogQueue();
        }

        /// <summary>
        /// 显示 Alert 对话框
        /// </summary>
        public void Alert(string message, string title = null, Action onClose = null)
        {
            var config = DialogConfig.Alert(message, title);
            ShowDialog(config, _ => onClose?.Invoke());
        }

        /// <summary>
        /// 显示 Confirm 对话框
        /// </summary>
        public void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            var config = DialogConfig.Confirm(message, title);
            ShowDialog(config, result => onResult?.Invoke(result.IsConfirmed));
        }

        /// <summary>
        /// 显示 Prompt 对话框
        /// </summary>
        public void Prompt(string message, string title = null, string defaultValue = null,
            Action<bool, string> onResult = null)
        {
            var config = PromptConfig.Create(message, title, defaultValue);
            var panelType = mDefaultPromptType != default ? mDefaultPromptType : mDefaultDialogType;
            ShowDialog(panelType, config, result => onResult?.Invoke(result.IsConfirmed, result.InputValue));
        }

        #endregion

        #region 对话框队列

        private void ProcessDialogQueue()
        {
            if (mIsDialogProcessing || mDialogQueue.Count == 0) return;
            if (mCurrentDialog != default) return;

            mIsDialogProcessing = true;
            var item = mDialogQueue.Dequeue();

            var data = new DialogData
            {
                Config = item.Config,
                OnResult = result =>
                {
                    item.OnResult?.Invoke(result);
                    OnDialogClosed();
                }
            };

            OpenPanelAsyncInternal(item.PanelType, item.Level, data, panel =>
            {
                mCurrentDialog = panel as UIDialogPanel;
                if (mCurrentDialog == default)
                {
                    KitLogger.Error($"[UIRoot] 无法创建对话框: {item.PanelType.Name}");
                    data.OnResult?.Invoke(new DialogResultData { Result = DialogResult.Cancel });
                }
            });
        }

        private void OnDialogClosed()
        {
            mCurrentDialog = null;
            mIsDialogProcessing = false;
            ProcessDialogQueue();
        }

        #endregion

        #region 对话框查询

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public bool HasActiveDialog => mCurrentDialog != default;

        /// <summary>
        /// 队列中的对话框数量
        /// </summary>
        public int DialogQueueCount => mDialogQueue.Count;

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public void ClearDialogQueue()
        {
            while (mDialogQueue.Count > 0)
            {
                var item = mDialogQueue.Dequeue();
                item.OnResult?.Invoke(new DialogResultData { Result = DialogResult.Cancel });
            }
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 对话框

        /// <summary>
        /// [UniTask] 显示对话框
        /// </summary>
        public UniTask<DialogResultData> ShowDialogUniTaskAsync(DialogConfig config,
            CancellationToken ct = default)
        {
            return ShowDialogUniTaskAsync(mDefaultDialogType, config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public UniTask<DialogResultData> ShowDialogUniTaskAsync<T>(DialogConfig config,
            CancellationToken ct = default) where T : UIDialogPanel
        {
            return ShowDialogUniTaskAsync(typeof(T), config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public UniTask<DialogResultData> ShowDialogUniTaskAsync(Type panelType, DialogConfig config,
            CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource<DialogResultData>();
            ct.Register(() => tcs.TrySetCanceled());
            ShowDialog(panelType, config, result => tcs.TrySetResult(result));
            return tcs.Task;
        }

        /// <summary>
        /// [UniTask] Alert 对话框
        /// </summary>
        public async UniTask AlertUniTaskAsync(string message, string title = null,
            CancellationToken ct = default)
        {
            var config = DialogConfig.Alert(message, title);
            await ShowDialogUniTaskAsync(config, ct);
        }

        /// <summary>
        /// [UniTask] Confirm 对话框
        /// </summary>
        public async UniTask<bool> ConfirmUniTaskAsync(string message, string title = null,
            CancellationToken ct = default)
        {
            var config = DialogConfig.Confirm(message, title);
            var result = await ShowDialogUniTaskAsync(config, ct);
            return result.IsConfirmed;
        }

        /// <summary>
        /// [UniTask] Prompt 对话框
        /// </summary>
        public async UniTask<(bool confirmed, string value)> PromptUniTaskAsync(string message,
            string title = null, string defaultValue = null, CancellationToken ct = default)
        {
            var config = PromptConfig.Create(message, title, defaultValue);
            var panelType = mDefaultPromptType != default ? mDefaultPromptType : mDefaultDialogType;
            var result = await ShowDialogUniTaskAsync(panelType, config, ct);
            return (result.IsConfirmed, result.InputValue);
        }

        #endregion
#endif
    }
}
