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
                Description = "焦点系统集成在 UIRoot 中，支持鼠标/触摸和手柄/键盘两种输入模式自动切换。默认禁用，需手动启用。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用焦点系统",
                        Code = @"// 启用焦点系统（默认禁用）
UIKit.FocusSystemEnabled = true;

// 检查是否启用
if (UIKit.FocusSystemEnabled)
{
    Debug.Log(""焦点系统已启用"");
}",
                        Explanation = "焦点系统默认禁用，适合纯鼠标/触摸游戏。手柄支持需要启用。"
                    },
                    new()
                    {
                        Title = "焦点控制",
                        Code = @"// 设置焦点
UIKit.SetFocus(myButton);
UIKit.SetFocus(myButton.gameObject);

// 清除焦点
UIKit.ClearFocus();

// 获取当前焦点
var focus = UIKit.GetCurrentFocus();
if (focus != default)
{
    Debug.Log($""当前焦点: {focus.name}"");
}",
                        Explanation = "使用 == default 判空，禁止 ?. 操作符。"
                    },
                    new()
                    {
                        Title = "输入模式",
                        Code = @"// 获取当前输入模式
var mode = UIKit.GetInputMode();
// UIInputMode.Pointer: 鼠标/触摸
// UIInputMode.Navigation: 手柄/键盘

// 监听模式切换
EventKit.Type.Register<InputModeChangedEvent>(e =>
{
    if (e.Current == UIInputMode.Navigation)
    {
        ShowGamepadHints();
    }
    else
    {
        HideGamepadHints();
    }
}).UnRegisterWhenGameObjectDestroyed(gameObject);",
                        Explanation = "系统自动检测：鼠标移动切换 Pointer，手柄/键盘输入切换 Navigation。"
                    },
                    new()
                    {
                        Title = "面板焦点配置",
                        Code = @"public class MyPanel : UIPanel
{
    // Inspector 中配置
    // [SerializeField] bool mAutoFocusOnShow = false;  // 默认禁用
    // [SerializeField] Selectable mDefaultSelectable;

    // 代码配置
    protected override void Awake()
    {
        base.Awake();
        SetAutoFocusOnShow(true);  // 启用自动焦点
        SetDefaultSelectable(myFirstButton);
    }

    // 获取默认焦点元素
    public Selectable GetDefaultSelectable() => mDefaultSelectable;
}",
                        Explanation = "面板级焦点配置，AutoFocusOnShow 默认禁用。"
                    },
                    new()
                    {
                        Title = "焦点事件",
                        Code = @"EventKit.Type.Register<FocusChangedEvent>(e =>
{
    var prev = e.Previous != default ? e.Previous.name : ""null"";
    var curr = e.Current != default ? e.Current.name : ""null"";
    Debug.Log($""焦点: {prev} → {curr}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);",
                        Explanation = "焦点变化事件包含前后焦点对象。"
                    }
                }
            };
        }
    }
}
#endif
