#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 对话框系统文档
    /// </summary>
    internal static class UIKitDocDialog
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "对话框系统",
                Description = "UIKit 提供完整的对话框系统，支持 Alert、Confirm、Prompt 等常用对话框。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "快捷对话框",
                        Code = @"// Alert 对话框
UIKit.Alert(""保存成功！"", ""提示"");

// Confirm 对话框
UIKit.Confirm(""确定要删除吗？"", ""警告"", confirmed =>
{
    if (confirmed) Debug.Log(""用户确认"");
});

// Prompt 对话框
UIKit.Prompt(""请输入名称"", ""创建"", ""默认值"", (confirmed, value) =>
{
    if (confirmed) Debug.Log($""输入: {value}"");
});",
                        Explanation = "快捷方法适合简单场景。"
                    },
                    new()
                    {
                        Title = "UniTask 异步对话框",
                        Code = @"// Alert
await UIKit.AlertUniTaskAsync(""操作完成！"");

// Confirm
bool confirmed = await UIKit.ConfirmUniTaskAsync(""确定要退出吗？"");

// Prompt
var (success, value) = await UIKit.PromptUniTaskAsync(""请输入名称"");",
                        Explanation = "UniTask 版本让对话框可以像普通异步方法一样使用。"
                    },
                    new()
                    {
                        Title = "自定义对话框配置",
                        Code = @"var config = new DialogConfig
{
    Title = ""确认购买"",
    Message = ""是否花费 100 金币？"",
    Buttons = DialogButtonType.YesNo,
    YesText = ""购买"",
    NoText = ""取消""
};

UIKit.ShowDialog(config, result =>
{
    if (result.IsConfirmed) PurchaseItem();
});",
                        Explanation = "DialogConfig 提供完整的配置选项。"
                    },
                    new()
                    {
                        Title = "注册默认对话框",
                        Code = @"// 注册默认对话框类型
UIKit.SetDefaultDialogType<MyDialogPanel>();

// 注册默认输入对话框
UIKit.SetDefaultPromptType<MyPromptPanel>();",
                        Explanation = "继承 UIDialogPanel 创建自定义对话框。"
                    }
                }
            };
        }
    }
}
#endif
