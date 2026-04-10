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
                Description = "UIKit 基于 Input System 提供手柄和键盘导航支持，集成在 UIRoot 焦点子系统中。默认禁用，需手动启用。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用手柄支持",
                        Code = @"// 启用焦点系统（必须先启用）
UIKit.FocusSystemEnabled = true;

// 检查当前输入模式
var mode = UIKit.GetInputMode();
if (mode == UIInputMode.Navigation)
{
    // 当前为手柄/键盘导航模式
}",
                        Explanation = "系统自动检测输入设备：鼠标移动切换到 Pointer 模式，手柄/键盘输入切换到 Navigation 模式。"
                    },
                    new()
                    {
                        Title = "焦点管理",
                        Code = @"// 设置焦点
UIKit.SetFocus(someButton);
UIKit.SetFocus(someButton.gameObject);

// 清除焦点
UIKit.ClearFocus();

// 获取当前焦点
var currentFocus = UIKit.GetCurrentFocus();
if (currentFocus != default)
{
    Debug.Log($""当前焦点: {currentFocus.name}"");
}

// 在面板中设置默认焦点
SetDefaultSelectable(mStartButton);
SetAutoFocusOnShow(true);",
                        Explanation = "面板显示时自动聚焦默认元素（需启用 AutoFocusOnShow）。使用 == default 判空。"
                    },
                    new()
                    {
                        Title = "监听导航事件",
                        Code = @"// 获取导航器（通过 UIRoot）
var navigator = UIRoot.Instance.Navigator;
if (navigator != default)
{
    // 监听取消键（默认弹出栈顶面板）
    navigator.OnCancel += () => Debug.Log(""取消键"");
    
    // 监听 Tab 切换（LB/RB）
    navigator.OnTabSwitch += dir => Debug.Log($""Tab: {dir}"");
    
    // 监听菜单键
    navigator.OnMenu += () => Debug.Log(""菜单键"");
}",
                        Explanation = "GamepadNavigator 提供导航事件回调。"
                    },
                    new()
                    {
                        Title = "GamepadConfig 配置",
                        Code = @"// 创建配置：Assets > Create > YokiFrame > UIKit > Gamepad Config

// 主要配置项：
// NavigationDeadzone: 摇杆死区（默认 0.5）
// NavigationRepeatDelay: 首次重复延迟（默认 0.4s）
// NavigationRepeatRate: 重复间隔（默认 0.1s）
// HideCursorOnGamepad: 手柄模式隐藏光标
// HighlightColor: 焦点高亮颜色

// 在 UIRoot 的 UIRootConfig 中配置 GamepadConfig 引用",
                        Explanation = "GamepadConfig 是 ScriptableObject，可在 Inspector 中配置。"
                    },
                    new()
                    {
                        Title = "输入模式变更事件",
                        Code = @"// 监听输入模式切换
EventKit.Type.Register<InputModeChangedEvent>(e =>
{
    if (e.Current == UIInputMode.Navigation)
    {
        ShowGamepadHints();  // 显示手柄提示
    }
    else
    {
        HideGamepadHints();  // 隐藏手柄提示
    }
}).UnRegisterWhenGameObjectDestroyed(gameObject);",
                        Explanation = "通过事件监听输入模式切换，动态显示/隐藏操作提示。"
                    }
                }
            };
        }
    }
}
#endif
