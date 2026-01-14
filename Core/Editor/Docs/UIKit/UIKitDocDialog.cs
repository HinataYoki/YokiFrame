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
                Description = "UIKit 提供 Alert、Confirm、Prompt 等常用对话框，支持回调和 UniTask 异步。对话框自动排队显示。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "快捷对话框",
                        Code = @"// Alert（提示）
UIKit.Alert(""保存成功！"", ""提示"");

// Confirm（确认）
UIKit.Confirm(""确定要删除吗？"", ""警告"", confirmed =>
{
    if (confirmed) DeleteItem();
});

// Prompt（输入）
UIKit.Prompt(""请输入名称"", ""创建"", ""默认值"", (confirmed, value) =>
{
    if (confirmed) CreateItem(value);
});",
                        Explanation = "快捷方法适合简单场景。"
                    },
                    new()
                    {
                        Title = "UniTask 异步",
                        Code = @"// Alert
await UIKit.AlertUniTaskAsync(""操作完成！"", ct: destroyCancellationToken);

// Confirm
bool confirmed = await UIKit.ConfirmUniTaskAsync(""确定退出？"", ct: destroyCancellationToken);
if (confirmed) QuitGame();

// Prompt
var (success, value) = await UIKit.PromptUniTaskAsync(""请输入名称"", ct: destroyCancellationToken);
if (success) CreateItem(value);",
                        Explanation = "UniTask 版本让对话框可以像普通异步方法使用。"
                    },
                    new()
                    {
                        Title = "自定义配置",
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
                        Explanation = "DialogConfig 提供完整配置选项。"
                    },
                    new()
                    {
                        Title = "自定义对话框类型",
                        Code = @"// 继承 UIDialogPanel 创建自定义对话框
public class MyDialog : UIDialogPanel
{
    protected override void SetupDialog(DialogConfig config)
    {
        // 设置 UI 内容
        mTitleLabel.text = config.Title;
        mMessageLabel.text = config.Message;
    }
}

// 注册为默认对话框
UIKit.SetDefaultDialogType<MyDialog>();

// 注册为默认输入对话框
UIKit.SetDefaultPromptType<MyPromptDialog>();

// 使用指定类型的对话框
UIKit.ShowDialog<MyDialog>(config, onResult);",
                        Explanation = "继承 UIDialogPanel 创建自定义样式的对话框。"
                    },
                    new()
                    {
                        Title = "对话框队列",
                        Code = @"// 检查是否有对话框正在显示
if (UIKit.HasActiveDialog)
{
    Debug.Log(""有对话框正在显示"");
}

// 清空对话框队列
UIKit.ClearDialogQueue();",
                        Explanation = "多个对话框会排队显示，不会同时出现。"
                    }
                }
            };
        }
    }
}
#endif
