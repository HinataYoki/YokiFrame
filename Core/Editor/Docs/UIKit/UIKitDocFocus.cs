#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 焦点系统文档
    /// </summary>
    internal static class UIKitDocFocus
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "焦点系统",
                Description = "UIFocusSystem 提供 UI 焦点管理，支持鼠标/触摸和手柄/键盘两种输入模式的自动切换，以及面板焦点记忆功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本焦点控制",
                        Code = @"// 获取焦点系统实例（注意：UnityEngine.Object 禁止使用 ?. 操作符）
var focusSystem = UIFocusSystem.Instance;
if (focusSystem == default) return;

// 设置焦点到指定对象
focusSystem.SetFocus(myButton.gameObject);
focusSystem.SetFocus(mySelectable);

// 清除当前焦点
focusSystem.ClearFocus();

// 恢复上次焦点
focusSystem.RestoreLastFocus();

// 获取当前焦点对象
GameObject current = focusSystem.CurrentFocus;",
                        Explanation = "焦点系统封装了 EventSystem 的焦点管理。注意：对 UnityEngine.Object 派生类型禁止使用 ?./?? 操作符，应使用 == default 判空。"
                    },
                    new()
                    {
                        Title = "输入模式",
                        Code = @"// 获取当前输入模式
UIInputMode mode = UIFocusSystem.Instance.CurrentInputMode;

// 输入模式：
// - UIInputMode.Pointer: 鼠标/触摸模式
// - UIInputMode.Navigation: 手柄/键盘导航模式

// 监听输入模式变化
EventKit.Type.Register<InputModeChangedEvent>(e =>
{
    if (e.Current == UIInputMode.Navigation)
    {
        // 切换到手柄模式，显示导航提示
        ShowNavigationHints();
    }
    else
    {
        // 切换到鼠标模式，隐藏导航提示
        HideNavigationHints();
    }
});",
                        Explanation = "系统自动检测输入设备切换，鼠标移动切换到 Pointer 模式，手柄/键盘输入切换到 Navigation 模式。"
                    },
                    new()
                    {
                        Title = "焦点变化事件",
                        Code = @"// 监听焦点变化（注意：UnityEngine.Object 禁止使用 ?. 操作符）
EventKit.Type.Register<FocusChangedEvent>(e =>
{
    // 正确的判空方式：使用 == default
    var prevName = e.Previous != default ? e.Previous.name : ""null"";
    var currName = e.Current != default ? e.Current.name : ""null"";
    Debug.Log($""焦点从 {prevName} 变为 {currName}"");
    
    // 获取焦点所在的面板
    if (e.Panel != default)
    {
        Debug.Log($""当前面板: {e.Panel.GetType().Name}"");
    }
});",
                        Explanation = "焦点变化事件包含前后焦点对象和所属面板信息。对 UnityEngine.Object 使用 == default 判空，避免 ?. 绕过 Unity 伪空检查。"
                    },
                    new()
                    {
                        Title = "面板焦点记忆",
                        Code = @"// 焦点系统自动记忆每个面板的最后焦点位置
// 当面板重新显示时，会自动恢复到上次的焦点

// 手动获取面板的记忆焦点
var focus = UIFocusSystem.Instance.GetPanelFocusMemory(panel);

// 手动设置面板的记忆焦点
UIFocusSystem.Instance.SetPanelFocusMemory(panel, myButton.gameObject);

// 面板生命周期中的焦点处理（UIPanel 内部已自动调用）
// OnPanelShow: 恢复记忆焦点或聚焦默认元素
// OnPanelHide: 保存当前焦点到记忆
// OnPanelClose: 清理焦点记忆",
                        Explanation = "面板焦点记忆让用户在面板间切换时保持焦点位置，提升手柄操作体验。"
                    },
                    new()
                    {
                        Title = "配置选项",
                        Code = @"var focusSystem = UIFocusSystem.Instance;
if (focusSystem == default) return;

// 启用/禁用自动焦点（面板显示时自动聚焦）
focusSystem.AutoFocusEnabled = true;

// 启用/禁用手柄支持
focusSystem.GamepadEnabled = true;

// 获取手柄导航器（注意判空）
var navigator = focusSystem.Navigator;
if (navigator != default)
{
    // 使用导航器
}

// 获取焦点高亮组件（注意判空）
var highlight = focusSystem.FocusHighlight;
if (highlight != default)
{
    // 使用高亮组件
}",
                        Explanation = "可通过 Inspector 或代码配置焦点系统行为。访问 UnityEngine.Object 子组件时需显式判空。"
                    }
                }
            };
        }
    }
}
#endif
