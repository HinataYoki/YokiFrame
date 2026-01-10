#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 手柄/键盘导航文档
    /// </summary>
    internal static class UIKitDocGamepad
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "手柄/键盘导航",
                Description = "UIKit 提供完整的手柄和键盘导航支持，基于 Input System 实现。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用手柄支持",
                        Code = @"// 手柄支持默认启用
UIKit.EnableGamepad();   // 启用
UIKit.DisableGamepad();  // 禁用

// 检查当前输入模式
if (UIKit.IsNavigationMode)
{
    // 当前为手柄/键盘导航模式
}

// 获取焦点系统时需判空（UnityEngine.Object 禁止使用 ?. 操作符）
var focusSystem = UIFocusSystem.Instance;
if (focusSystem != default)
{
    // 安全使用焦点系统
}",
                        Explanation = "系统会自动检测输入设备并切换模式。对 UnityEngine.Object 使用 == default 判空。"
                    },
                    new()
                    {
                        Title = "焦点管理",
                        Code = @"// 设置焦点（内部已处理判空）
UIKit.SetFocus(someButton);
UIKit.ClearFocus();

// 获取当前焦点（返回值可能为 default）
var currentFocus = UIKit.GetCurrentFocus();
if (currentFocus != default)
{
    // 安全使用焦点对象
}

// 在面板中设置默认焦点
SetDefaultSelectable(mStartButton);",
                        Explanation = "面板显示时会自动聚焦到默认元素。获取焦点对象后需使用 == default 判空。"
                    },
                    new()
                    {
                        Title = "GamepadConfig 配置",
                        Code = @"// 创建配置资源
// Assets > Create > YokiFrame > UIKit > Gamepad Config

// 配置项：
// - Navigation Deadzone: 摇杆死区
// - Navigation Repeat Delay: 首次重复延迟
// - Navigation Repeat Rate: 重复间隔
// - Hide Cursor On Gamepad: 手柄模式隐藏光标
// - Highlight Color: 焦点高亮颜色",
                        Explanation = "GamepadConfig 是 ScriptableObject 配置文件。"
                    },
                    new()
                    {
                        Title = "UITabGroup 组件",
                        Code = @"// UITabGroup 支持 LB/RB 切换标签页
mTabGroup.SelectTab(0);      // 选中指定 Tab
mTabGroup.NextTab();         // 下一个 Tab
mTabGroup.PreviousTab();     // 上一个 Tab

// 监听 Tab 切换
mTabGroup.OnTabChanged += OnTabChanged;",
                        Explanation = "UITabGroup 会自动响应 LB/RB 按键。"
                    },
                    new()
                    {
                        Title = "UIBackHandler 组件",
                        Code = @"// 自定义返回行为
var backHandler = gameObject.AddComponent<UIBackHandler>();
backHandler.Behavior = BackBehavior.Custom;
backHandler.OnCustomBack += () =>
{
    ShowConfirmDialog(""确定要退出吗？"");
};

// 行为选项：PopStack, ClosePanel, HidePanel, DoNothing, Custom",
                        Explanation = "UIBackHandler 可以覆盖默认的返回行为。"
                    }
                }
            };
        }
    }
}
#endif
