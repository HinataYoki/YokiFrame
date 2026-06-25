#if !GODOT
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
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
        private bool mIsDialogResetting;

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
        /// <param name="config">对话框配置</param>
        /// <param name="onResult">结果回调</param>
        public void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            ShowDialog(mDefaultDialogType, config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        /// <typeparam name="T">对话框类型</typeparam>
        /// <param name="config">对话框配置</param>
        /// <param name="onResult">结果回调</param>
        public void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null)
            where T : UIDialogPanel
        {
            ShowDialog(typeof(T), config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        /// <param name="panelType">对话框类型</param>
        /// <param name="config">对话框配置</param>
        /// <param name="onResult">结果回调</param>
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
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="onClose">关闭回调</param>
        public void Alert(string message, string title = null, Action onClose = null)
        {
            var config = DialogConfig.Alert(message, title);
            ShowDialog(config, _ => onClose?.Invoke());
        }

        /// <summary>
        /// 显示 Confirm 对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="onResult">结果回调</param>
        public void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            var config = DialogConfig.Confirm(message, title);
            ShowDialog(config, result => onResult?.Invoke(result.IsConfirmed));
        }

        /// <summary>
        /// 显示 Prompt 对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="onResult">结果回调</param>
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
            if (mIsDialogResetting) return;
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
#if YOKIFRAME_ZSTRING_SUPPORT
                    using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                    {
                        sb.Append("[UIRoot] 无法创建对话框: ");
                        sb.Append(item.PanelType.Name);
                        KitLogger.Error(sb.ToString());
                    }
#else
                    KitLogger.Error("[UIRoot] 无法创建对话框: " + item.PanelType.Name);
#endif
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

        internal void ResetDialogState()
        {
            mIsDialogResetting = true;
            mCurrentDialog = null;
            mIsDialogProcessing = false;
            ClearDialogQueue();
            mIsDialogResetting = false;
        }

        internal void BeginDialogReset()
        {
            mIsDialogResetting = true;
            mCurrentDialog = null;
            mIsDialogProcessing = false;
            ClearDialogQueue();
        }

        internal void EndDialogReset()
        {
            ClearDialogQueue();
            mCurrentDialog = null;
            mIsDialogProcessing = false;
            mIsDialogResetting = false;
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

        #region 异步对话框

        /// <summary>
        /// 异步显示对话框。安装 UniTask 后返回 UniTask，否则返回 Task。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<DialogResultData> ShowDialogAsync(DialogConfig config,
            CancellationToken ct = default)
#else
        public Task<DialogResultData> ShowDialogAsync(DialogConfig config,
            CancellationToken ct = default)
#endif
        {
            return ShowDialogAsync(mDefaultDialogType, config, ct);
        }

        /// <summary>
        /// 异步显示指定类型的对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<DialogResultData> ShowDialogAsync<T>(DialogConfig config,
            CancellationToken ct = default) where T : UIDialogPanel
#else
        public Task<DialogResultData> ShowDialogAsync<T>(DialogConfig config,
            CancellationToken ct = default) where T : UIDialogPanel
#endif
        {
            return ShowDialogAsync(typeof(T), config, ct);
        }

        /// <summary>
        /// 异步显示指定类型的对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<DialogResultData> ShowDialogAsync(Type panelType, DialogConfig config,
            CancellationToken ct)
#else
        public Task<DialogResultData> ShowDialogAsync(Type panelType, DialogConfig config,
            CancellationToken ct)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var tcs = new UniTaskCompletionSource<DialogResultData>();
            ct.Register(() => tcs.TrySetCanceled());
            ShowDialog(panelType, config, result => tcs.TrySetResult(result));
            return tcs.Task;
#else
            var tcs = new TaskCompletionSource<DialogResultData>();
            ct.Register(() => tcs.TrySetCanceled(ct));
            ShowDialog(panelType, config, result => tcs.TrySetResult(result));
            return tcs.Task;
#endif
        }

        /// <summary>
        /// 异步 Alert 对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask AlertAsync(string message, string title = null,
            CancellationToken ct = default)
#else
        public async Task AlertAsync(string message, string title = null,
            CancellationToken ct = default)
#endif
        {
            var config = DialogConfig.Alert(message, title);
#if YOKIFRAME_UNITASK_SUPPORT
            await ShowDialogAsync(config, ct);
#else
            await ShowDialogAsync(config, ct).ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// 异步 Confirm 对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<bool> ConfirmAsync(string message, string title = null,
            CancellationToken ct = default)
#else
        public async Task<bool> ConfirmAsync(string message, string title = null,
            CancellationToken ct = default)
#endif
        {
            var config = DialogConfig.Confirm(message, title);
#if YOKIFRAME_UNITASK_SUPPORT
            var result = await ShowDialogAsync(config, ct);
#else
            var result = await ShowDialogAsync(config, ct).ConfigureAwait(false);
#endif
            return result.IsConfirmed;
        }

        /// <summary>
        /// 异步 Prompt 对话框。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<(bool confirmed, string value)> PromptAsync(string message,
            string title = null, string defaultValue = null, CancellationToken ct = default)
#else
        public async Task<(bool confirmed, string value)> PromptAsync(string message,
            string title = null, string defaultValue = null, CancellationToken ct = default)
#endif
        {
            var config = PromptConfig.Create(message, title, defaultValue);
            var panelType = mDefaultPromptType != default ? mDefaultPromptType : mDefaultDialogType;
#if YOKIFRAME_UNITASK_SUPPORT
            var result = await ShowDialogAsync(panelType, config, ct);
#else
            var result = await ShowDialogAsync(panelType, config, ct).ConfigureAwait(false);
#endif
            return (result.IsConfirmed, result.InputValue);
        }

        #endregion
    }
}
#endif
