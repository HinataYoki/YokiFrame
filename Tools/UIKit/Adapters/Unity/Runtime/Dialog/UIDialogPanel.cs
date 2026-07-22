#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>UIKit 对话框面板基类，负责结果幂等提交和模态所有权。</summary>
    public abstract class UIDialogPanel : UIPanel
    {
        private DialogConfig mConfig;
        private Action<DialogResultData> mOnResult;
        private bool mResultSent;

        /// <summary>当前对话框配置；仅在生命周期回调期间有效。</summary>
        protected DialogConfig Config => mConfig;

        /// <summary>初始化时读取队列提交的配置。</summary>
        protected override void OnInit(IUIData data = null)
        {
            ApplyDialogData(data);
        }

        /// <summary>每次打开时重置结果并调用具体面板的内容绑定。</summary>
        protected override void OnOpen(IUIData data = null)
        {
            ApplyDialogData(data);
            mResultSent = false;
            if (mConfig != null) SetupDialog(mConfig);
            UIKit.SetPanelModal(this, true);
        }

        /// <summary>关闭前未提交结果时补发 Cancel，避免异步调用永久等待。</summary>
        protected override void OnClose()
        {
            if (!mResultSent) SendResult(DialogResult.Cancel);
            UIKit.SetPanelModal(this, false);
        }

        /// <summary>由派生类将配置绑定到标题、消息、输入框和按钮。</summary>
        protected abstract void SetupDialog(DialogConfig config);

        /// <summary>提交一次结果并关闭当前对话框。</summary>
        protected void SendResult(DialogResult result, string inputValue = null, object customData = null)
        {
            if (mResultSent) return;
            mResultSent = true;
            DialogResultData resultData = new()
            {
                Result = result,
                InputValue = inputValue,
                CustomData = customData ?? (mConfig != null ? mConfig.CustomData : null)
            };
            Action<DialogResultData> callback = mOnResult;
            mOnResult = null;
            if (callback != null) callback(resultData);
        }

        /// <summary>供按钮事件提交 OK 结果。</summary>
        protected virtual void OnOKClicked() => SendAndClose(DialogResult.OK);

        /// <summary>供按钮事件提交 Cancel 结果。</summary>
        protected virtual void OnCancelClicked() => SendAndClose(DialogResult.Cancel);

        /// <summary>供按钮事件提交 Yes 结果。</summary>
        protected virtual void OnYesClicked() => SendAndClose(DialogResult.Yes);

        /// <summary>供按钮事件提交 No 结果。</summary>
        protected virtual void OnNoClicked() => SendAndClose(DialogResult.No);

        /// <summary>按配置决定是否响应背景点击。</summary>
        protected virtual void OnBackgroundClicked()
        {
            if (mConfig != null && mConfig.CloseOnBackgroundClick) SendAndClose(mConfig.BackgroundClickResult);
        }

        /// <summary>配置按钮显隐、文本和点击回调。</summary>
        protected void ConfigureButton(Button button, DialogButtonType type, string customText, Action onClick)
        {
            if (button == null) return;
            bool visible = mConfig != null && (mConfig.Buttons & type) != 0;
            button.gameObject.SetActive(visible);
            if (!visible) return;
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null && !string.IsNullOrEmpty(customText)) text.text = customText;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }

        /// <summary>读取 Controller 传入的 DialogData。</summary>
        private void ApplyDialogData(IUIData data)
        {
            if (!(data is DialogData dialogData)) return;
            mConfig = dialogData.Config;
            mOnResult = dialogData.OnResult;
        }

        /// <summary>提交结果并请求 UIKit 关闭当前对话框。</summary>
        private void SendAndClose(DialogResult result)
        {
            SendResult(result);
            CloseSelf();
        }
    }

    /// <summary>对话框面板初始化数据。</summary>
    public sealed class DialogData : IUIData
    {
        /// <summary>对话框配置。</summary>
        public DialogConfig Config { get; set; }

        /// <summary>结果回调。</summary>
        public Action<DialogResultData> OnResult { get; set; }
    }
}
#endif
