#if UNITY_2022_3_OR_NEWER
using System;

namespace YokiFrame
{
    /// <summary>对话框可显示的按钮集合。</summary>
    [Flags]
    public enum DialogButtonType
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 4,
        No = 8,
        OKCancel = OK | Cancel,
        YesNo = Yes | No,
        YesNoCancel = Yes | No | Cancel
    }

    /// <summary>对话框最终结果。</summary>
    public enum DialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
        Custom
    }

    /// <summary>描述标题、消息、按钮和背景关闭行为的对话框配置。</summary>
    public class DialogConfig
    {
        /// <summary>对话框标题。</summary>
        public string Title { get; set; }

        /// <summary>对话框消息。</summary>
        public string Message { get; set; }

        /// <summary>要显示的按钮集合。</summary>
        public DialogButtonType Buttons { get; set; } = DialogButtonType.OK;

        /// <summary>自定义 OK 按钮文本。</summary>
        public string OKText { get; set; }

        /// <summary>自定义 Cancel 按钮文本。</summary>
        public string CancelText { get; set; }

        /// <summary>自定义 Yes 按钮文本。</summary>
        public string YesText { get; set; }

        /// <summary>自定义 No 按钮文本。</summary>
        public string NoText { get; set; }

        /// <summary>是否允许点击背景关闭。</summary>
        public bool CloseOnBackgroundClick { get; set; }

        /// <summary>背景关闭时提交的结果。</summary>
        public DialogResult BackgroundClickResult { get; set; } = DialogResult.Cancel;

        /// <summary>调用方关联的业务数据。</summary>
        public object CustomData { get; set; }

        /// <summary>创建一个 Alert 配置。</summary>
        public static DialogConfig Alert(string message, string title = null)
        {
            return new DialogConfig { Title = title, Message = message, Buttons = DialogButtonType.OK };
        }

        /// <summary>创建一个 Confirm 配置。</summary>
        public static DialogConfig Confirm(string message, string title = null)
        {
            return new DialogConfig { Title = title, Message = message, Buttons = DialogButtonType.OKCancel };
        }

        /// <summary>创建一个 Yes/No 配置。</summary>
        public static DialogConfig YesNo(string message, string title = null)
        {
            return new DialogConfig { Title = title, Message = message, Buttons = DialogButtonType.YesNo };
        }
    }

    /// <summary>扩展输入框内容的 Prompt 配置。</summary>
    public sealed class PromptConfig : DialogConfig
    {
        /// <summary>输入框占位文本。</summary>
        public string Placeholder { get; set; }

        /// <summary>输入框初始值。</summary>
        public string DefaultValue { get; set; }

        /// <summary>输入值校验器。</summary>
        public Func<string, bool> Validator { get; set; }

        /// <summary>校验失败时显示的消息。</summary>
        public string ValidationErrorMessage { get; set; }

        /// <summary>输入最大长度，零表示不限制。</summary>
        public int MaxLength { get; set; }

        /// <summary>是否使用密码输入模式。</summary>
        public bool IsPassword { get; set; }

        /// <summary>创建一个 Prompt 配置。</summary>
        public static PromptConfig Create(string message, string title = null, string defaultValue = null)
        {
            return new PromptConfig
            {
                Title = title,
                Message = message,
                DefaultValue = defaultValue,
                Buttons = DialogButtonType.OKCancel
            };
        }
    }

    /// <summary>传递给对话框回调的结果数据。</summary>
    public sealed class DialogResultData
    {
        /// <summary>结果枚举。</summary>
        public DialogResult Result { get; set; }

        /// <summary>Prompt 输入值。</summary>
        public string InputValue { get; set; }

        /// <summary>原始自定义数据。</summary>
        public object CustomData { get; set; }

        /// <summary>判断结果是否为 OK 或 Yes。</summary>
        public bool IsConfirmed => Result == DialogResult.OK || Result == DialogResult.Yes;

        /// <summary>判断结果是否为 Cancel 或 No。</summary>
        public bool IsCancelled => Result == DialogResult.Cancel || Result == DialogResult.No;
    }
}
#endif
