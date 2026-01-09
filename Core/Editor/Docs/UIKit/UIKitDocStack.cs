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
                Description = "UIKit 提供增强的面板堆栈管理，支持多命名栈、焦点自动管理、异步操作。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本堆栈操作",
                        Code = @"// 打开并压入堆栈
UIKit.PushOpenPanel<SettingsPanel>();

// 弹出面板
var panel = UIKit.PopPanel();

// 查看栈顶面板（不移除）
var topPanel = UIKit.PeekPanel();

// 获取栈深度
int depth = UIKit.GetStackDepth();

// 清空堆栈
UIKit.ClearStack(closeAll: true);",
                        Explanation = "堆栈模式适合设置页面、背包等需要返回上一级的场景。"
                    },
                    new()
                    {
                        Title = "多命名栈",
                        Code = @"// 使用不同的栈管理不同类型的面板
UIKit.PushPanel(mainPanel, ""main"");
UIKit.PushPanel(dialogPanel, ""dialog"");

// 从指定栈弹出
var panel = UIKit.PopPanel(""dialog"");

// 清空指定栈
UIKit.ClearStack(""dialog"", closeAll: true);",
                        Explanation = "多命名栈适合复杂 UI 场景，如同时存在主界面和弹窗。"
                    }
                }
            };
        }
    }
}
#endif
