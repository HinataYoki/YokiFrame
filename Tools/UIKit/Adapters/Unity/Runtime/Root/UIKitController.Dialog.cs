#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        private readonly Queue<DialogQueueItem> mDialogQueue = new();
        private Type mDefaultDialogType;
        private Type mDefaultPromptType;
        private UIDialogPanel mCurrentDialog;
        private bool mDialogProcessing;

        /// <summary>登记默认 Dialog 类型。</summary>
        internal void SetDefaultDialogType(Type dialogType)
        {
            ValidateDialogType(dialogType);
            mDefaultDialogType = dialogType;
        }

        /// <summary>登记默认 Prompt 类型。</summary>
        internal void SetDefaultPromptType(Type dialogType)
        {
            ValidateDialogType(dialogType);
            mDefaultPromptType = dialogType;
        }

        /// <summary>入队一个对话框请求并尝试启动队首。</summary>
        internal void ShowDialog(Type panelType, DialogConfig config, Action<DialogResultData> onResult)
        {
            if (!IsDialogType(panelType))
            {
                InvokeDialogResult(onResult, DialogResult.Cancel, config);
                return;
            }
            mDialogQueue.Enqueue(new DialogQueueItem(panelType, config, onResult));
            ProcessDialogQueue();
        }

        /// <summary>提交异步对话框请求；取消只影响当前等待者。</summary>
        internal Task<DialogResultData> ShowDialogAsync(Type panelType, DialogConfig config, CancellationToken token)
        {
            TaskCompletionSource<DialogResultData> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ShowDialog(panelType, config, result => completion.TrySetResult(result));
            return AwaitDialogResultAsync(completion, token);
        }

        /// <summary>等待 Dialog 结果，并在完成或取消后释放 CancellationToken 注册。</summary>
        private static async Task<DialogResultData> AwaitDialogResultAsync(
            TaskCompletionSource<DialogResultData> completion,
            CancellationToken token)
        {
            using CancellationTokenRegistration registration = token.Register(
                static state => ((TaskCompletionSource<DialogResultData>)state).TrySetCanceled(),
                completion);
            return await completion.Task;
        }

        /// <summary>取消所有尚未显示的队列项。</summary>
        internal void ClearDialogQueue()
        {
            while (mDialogQueue.Count > 0)
            {
                DialogQueueItem item = mDialogQueue.Dequeue();
                InvokeDialogResult(item.OnResult, DialogResult.Cancel, item.Config);
            }
        }

        /// <summary>获取当前是否存在活动 Dialog。</summary>
        internal bool HasActiveDialog => mCurrentDialog != null;

        /// <summary>获取尚未显示的队列项数量。</summary>
        internal int DialogQueueCount => mDialogQueue.Count;

        /// <summary>读取默认 Dialog 类型。</summary>
        internal Type DefaultDialogType => mDefaultDialogType;

        /// <summary>读取默认 Prompt 类型。</summary>
        internal Type DefaultPromptType => mDefaultPromptType;

        /// <summary>启动队首请求并为当前 Dialog 注册关闭回调。</summary>
        private void ProcessDialogQueue()
        {
            if (mDialogProcessing || mCurrentDialog != null || mDialogQueue.Count == 0) return;
            mDialogProcessing = true;
            DialogQueueItem item = mDialogQueue.Dequeue();
            try
            {
                DialogData data = new()
                {
                    Config = item.Config,
                    OnResult = item.OnResult
                };
                UIPanel panel = Open(item.PanelType, UILevel.Pop, data, null, PanelCachePolicy.Reusable);
                mCurrentDialog = panel as UIDialogPanel;
                if (mCurrentDialog == null)
                {
                    Close(panel);
                    InvokeDialogResult(item.OnResult, DialogResult.Cancel, item.Config);
                    mDialogProcessing = false;
                    ProcessDialogQueue();
                    return;
                }
                mCurrentDialog.OnClosed(OnDialogClosed);
                SetModal(mCurrentDialog, true);
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                InvokeDialogResult(item.OnResult, DialogResult.Cancel, item.Config);
                mDialogProcessing = false;
                ProcessDialogQueue();
            }
        }

        /// <summary>当前 Dialog 完成关闭后启动下一项。</summary>
        private void OnDialogClosed()
        {
            mCurrentDialog = null;
            mDialogProcessing = false;
            ProcessDialogQueue();
        }

        /// <summary>校验类型继承 UIDialogPanel 且可实例化。</summary>
        private static void ValidateDialogType(Type panelType)
        {
            if (!IsDialogType(panelType)) throw new ArgumentException("Dialog type must derive from UIDialogPanel.", nameof(panelType));
        }

        /// <summary>判断类型是否为非抽象 UIDialogPanel。</summary>
        private static bool IsDialogType(Type panelType)
        {
            return panelType != null && typeof(UIDialogPanel).IsAssignableFrom(panelType) && !panelType.IsAbstract;
        }

        /// <summary>构造取消结果并隔离业务回调异常。</summary>
        private static void InvokeDialogResult(Action<DialogResultData> callback, DialogResult result, DialogConfig config)
        {
            if (callback == null) return;
            try
            {
                callback(new DialogResultData { Result = result, CustomData = config != null ? config.CustomData : null });
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
            }
        }

        /// <summary>保存一个待显示的 Dialog 请求。</summary>
        private sealed class DialogQueueItem
        {
            internal DialogQueueItem(Type panelType, DialogConfig config, Action<DialogResultData> onResult)
            {
                PanelType = panelType;
                Config = config;
                OnResult = onResult;
            }

            internal Type PanelType { get; }
            internal DialogConfig Config { get; }
            internal Action<DialogResultData> OnResult { get; }
        }
    }
}
#endif
