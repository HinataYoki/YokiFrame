#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 面板堆栈文档
    /// </summary>
    internal static class UIKitDocStack
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "面板堆栈",
                Description = "UIKit 提供面板堆栈管理，支持多命名栈、焦点自动管理、异步操作。适合设置页面、背包等需要返回上一级的场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本操作",
                        Code = @"// 打开并压入堆栈（自动隐藏前一面板）
UIKit.PushOpenPanel<SettingsPanel>();

// 弹出面板（自动显示前一面板并关闭弹出面板）
var panel = UIKit.PopPanel();

// 查看栈顶（不移除）
var top = UIKit.PeekPanel();

// 获取栈深度
int depth = UIKit.GetStackDepth();

// 清空堆栈
UIKit.ClearStack(closeAll: true);",
                        Explanation = "Push 自动隐藏前一面板，Pop 自动显示前一面板。"
                    },
                    new()
                    {
                        Title = "控制显示/关闭行为",
                        Code = @"// 压入时不隐藏前一面板
UIKit.PushOpenPanel<OverlayPanel>(hidePreLevel: false);

// 弹出时不显示前一面板
UIKit.PopPanel(showPreLevel: false);

// 弹出时不关闭面板（仅移出栈）
UIKit.PopPanel(autoClose: false);

// 压入已存在的面板
var panel = UIKit.GetPanel<SettingsPanel>();
if (panel != default)
{
    UIKit.PushPanel(panel, hidePreLevel: true);
}",
                        Explanation = "通过参数控制 Push/Pop 的显示和关闭行为。"
                    },
                    new()
                    {
                        Title = "多命名栈",
                        Code = @"// 使用不同栈管理不同类型面板
UIKit.PushPanel(mainPanel, ""main"");
UIKit.PushPanel(dialogPanel, ""dialog"");

// 从指定栈弹出
var panel = UIKit.PopPanel(""dialog"");

// 查看指定栈
var top = UIKit.PeekPanel(""dialog"");
int depth = UIKit.GetStackDepth(""dialog"");

// 清空指定栈
UIKit.ClearStack(""dialog"", closeAll: true);

// 获取所有栈名称
var names = UIKit.GetAllStackNames();",
                        Explanation = "多命名栈适合复杂 UI 场景，如同时存在主界面栈和弹窗栈。"
                    },
                    new()
                    {
                        Title = "UniTask 异步操作",
                        Code = @"// 异步打开并压入
var panel = await UIKit.PushOpenPanelUniTaskAsync<SettingsPanel>(
    UILevel.Common, data, hidePreLevel: true, ct);

// 异步弹出（等待动画完成）
var popped = await UIKit.PopPanelUniTaskAsync(
    showPreLevel: true, autoClose: true, ct);",
                        Explanation = "UniTask 版本等待动画完成后返回。"
                    },
                    new()
                    {
                        Title = "焦点钩子",
                        Code = @"public class StackPanel : UIPanel
{
    protected override void OnFocus()
    {
        // 成为栈顶时调用
        base.OnFocus();
        EnableInput();
    }

    protected override void OnBlur()
    {
        // 失去栈顶时调用
        base.OnBlur();
        DisableInput();
    }

    protected override void OnResume()
    {
        // Pop 后恢复时调用
        base.OnResume();
        RefreshData();
    }
}",
                        Explanation = "焦点钩子自动管理面板焦点状态。"
                    }
                }
            };
        }
    }
}
#endif
