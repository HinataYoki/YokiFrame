using System;

namespace YokiFrame
{
    /// <summary>
    /// 对话框按钮类型
    /// </summary>
    public enum DialogButtonType
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 4,
        No = 8,
        
        // 常用组合
        OKCancel = OK | Cancel,
        YesNo = Yes | No,
        YesNoCancel = Yes | No | Cancel
    }

    /// <summary>
    /// 对话框结果
    /// </summary>
    public enum DialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
        Custom
    }

    /// <summary>
    /// 对话框配置
    /// </summary>
    public class DialogConfig
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 按钮类型
        /// </summary>
        public DialogButtonType Buttons { get; set; } = DialogButtonType.OK;

        /// <summary>
        /// 自定义按钮文本（覆盖默认文本）
        /// </summary>
        public string OKText { get; set; }
        public string CancelText { get; set; }
        public string YesText { get; set; }
        public string NoText { get; set; }

        /// <summary>
        /// 是否可以点击背景关闭
        /// </summary>
        public bool CloseOnBackgroundClick { get; set; } = false;

        /// <summary>
        /// 背景点击时的结果
        /// </summary>
        public DialogResult BackgroundClickResult { get; set; } = DialogResult.Cancel;

        /// <summary>
        /// 自定义数据
        /// </summary>
        public object CustomData { get; set; }

        /// <summary>
        /// 创建 Alert 配置
        /// </summary>
        public static DialogConfig Alert(string message, string title = null)
        {
            return new DialogConfig
            {
                Title = title,
                Message = message,
                Buttons = DialogButtonType.OK
            };
        }

        /// <summary>
        /// 创建 Confirm 配置
        /// </summary>
        public static DialogConfig Confirm(string message, string title = null)
        {
            return new DialogConfig
            {
                Title = title,
                Message = message,
                Buttons = DialogButtonType.OKCancel
            };
        }

        /// <summary>
        /// 创建 YesNo 配置
        /// </summary>
        public static DialogConfig YesNo(string message, string title = null)
        {
            return new DialogConfig
            {
                Title = title,
                Message = message,
                Buttons = DialogButtonType.YesNo
            };
        }
    }

    /// <summary>
    /// 输入对话框配置
    /// </summary>
    public class PromptConfig : DialogConfig
    {
        /// <summary>
        /// 输入框占位符
        /// </summary>
        public string Placeholder { get; set; }

        /// <summary>
        /// 默认输入值
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// 输入验证器
        /// </summary>
        public Func<string, bool> Validator { get; set; }

        /// <summary>
        /// 验证失败提示
        /// </summary>
        public string ValidationErrorMessage { get; set; }

        /// <summary>
        /// 最大输入长度
        /// </summary>
        public int MaxLength { get; set; } = 0;

        /// <summary>
        /// 是否为密码输入
        /// </summary>
        public bool IsPassword { get; set; } = false;

        /// <summary>
        /// 创建 Prompt 配置
        /// </summary>
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

    /// <summary>
    /// 对话框结果数据
    /// </summary>
    public class DialogResultData
    {
        /// <summary>
        /// 结果类型
        /// </summary>
        public DialogResult Result { get; set; }

        /// <summary>
        /// 输入值（用于 Prompt）
        /// </summary>
        public string InputValue { get; set; }

        /// <summary>
        /// 自定义数据
        /// </summary>
        public object CustomData { get; set; }

        /// <summary>
        /// 是否确认（OK/Yes）
        /// </summary>
        public bool IsConfirmed => Result is DialogResult.OK or DialogResult.Yes;

        /// <summary>
        /// 是否取消（Cancel/No）
        /// </summary>
        public bool IsCancelled => Result is DialogResult.Cancel or DialogResult.No;
    }
}
