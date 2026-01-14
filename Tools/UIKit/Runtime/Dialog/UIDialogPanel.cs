using System;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 对话框面板数据
    /// </summary>
    public class DialogData : IUIData
    {
        public DialogConfig Config { get; set; }
        public Action<DialogResultData> OnResult { get; set; }
    }

    /// <summary>
    /// 对话框面板基类
    /// </summary>
    public abstract class UIDialogPanel : UIPanel
    {
        #region 配置

        protected DialogConfig mConfig;
        protected Action<DialogResultData> mOnResult;
        protected bool mResultSent;

        #endregion

        #region 生命周期

        protected override void OnInit(IUIData data = null)
        {
            base.OnInit(data);

            if (data is DialogData dialogData)
            {
                mConfig = dialogData.Config;
                mOnResult = dialogData.OnResult;
            }
        }

        protected override void OnOpen(IUIData data = null)
        {
            base.OnOpen(data);

            mResultSent = false;

            if (data is DialogData dialogData)
            {
                mConfig = dialogData.Config;
                mOnResult = dialogData.OnResult;
            }

            if (mConfig != null)
            {
                SetupDialog(mConfig);
            }

            // 设置为模态
            if (Handler != null)
            {
                Handler.IsModal = true;
                UIRoot.Instance.SetPanelModal(this, true);
            }
        }

        protected override void OnClose()
        {
            // 如果没有发送结果，发送取消
            if (!mResultSent)
            {
                SendResult(DialogResult.Cancel);
            }

            // 移除模态
            if (Handler != null)
            {
                UIRoot.Instance.SetPanelModal(this, false);
            }

            base.OnClose();
        }

        #endregion

        #region 抽象方法

        /// <summary>
        /// 设置对话框内容
        /// </summary>
        protected abstract void SetupDialog(DialogConfig config);

        #endregion

        #region 结果处理

        /// <summary>
        /// 发送结果
        /// </summary>
        protected void SendResult(DialogResult result, string inputValue = null, object customData = null)
        {
            if (mResultSent) return;
            mResultSent = true;

            var resultData = new DialogResultData
            {
                Result = result,
                InputValue = inputValue,
                CustomData = customData ?? mConfig?.CustomData
            };

            mOnResult?.Invoke(resultData);

            // 触发事件
            EventKit.Type.Send(new DialogResultEvent
            {
                Panel = this,
                Result = resultData
            });
        }

        /// <summary>
        /// OK 按钮点击
        /// </summary>
        protected virtual void OnOKClicked()
        {
            SendResult(DialogResult.OK);
            CloseSelf();
        }

        /// <summary>
        /// Cancel 按钮点击
        /// </summary>
        protected virtual void OnCancelClicked()
        {
            SendResult(DialogResult.Cancel);
            CloseSelf();
        }

        /// <summary>
        /// Yes 按钮点击
        /// </summary>
        protected virtual void OnYesClicked()
        {
            SendResult(DialogResult.Yes);
            CloseSelf();
        }

        /// <summary>
        /// No 按钮点击
        /// </summary>
        protected virtual void OnNoClicked()
        {
            SendResult(DialogResult.No);
            CloseSelf();
        }

        /// <summary>
        /// 背景点击
        /// </summary>
        protected virtual void OnBackgroundClicked()
        {
            if (mConfig?.CloseOnBackgroundClick == true)
            {
                SendResult(mConfig.BackgroundClickResult);
                CloseSelf();
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 配置按钮
        /// </summary>
        protected void ConfigureButton(Button button, DialogButtonType type, string customText, Action onClick)
        {
            if (button == null) return;

            bool shouldShow = (mConfig?.Buttons & type) != 0;
            button.gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                // 设置文本（支持 TMP 和 Legacy Text）
                if (!string.IsNullOrEmpty(customText))
                {
                    var tmpText = button.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (tmpText != null)
                    {
                        tmpText.text = customText;
                    }
                }

                // 绑定点击
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        /// <summary>
        /// 获取默认按钮文本
        /// </summary>
        protected static string GetDefaultButtonText(DialogButtonType type)
        {
            return type switch
            {
                DialogButtonType.OK => "确定",
                DialogButtonType.Cancel => "取消",
                DialogButtonType.Yes => "是",
                DialogButtonType.No => "否",
                _ => ""
            };
        }

        #endregion
    }

    /// <summary>
    /// 对话框结果事件
    /// </summary>
    public struct DialogResultEvent
    {
        public UIDialogPanel Panel;
        public DialogResultData Result;
    }
}
