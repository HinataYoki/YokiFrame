#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 生命周期文档
    /// </summary>
    internal static class UIKitDocLifecycle
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "生命周期钩子",
                Description = "UIPanel 提供完整的生命周期钩子，支持动画前后回调和焦点管理。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础生命周期",
                        Code = @"public class MyPanel : UIPanel
{
    protected override void OnInit(IUIData data = null)
    {
        // 首次创建时调用，只调用一次
        // 用于：绑定事件、初始化组件
    }

    protected override void OnOpen(IUIData data = null)
    {
        // 每次打开时调用
        // 用于：刷新数据、重置状态
    }

    protected override void OnShow()
    {
        // 显示时调用（动画过程中）
    }

    protected override void OnHide()
    {
        // 隐藏时调用（动画过程中）
    }

    protected override void OnClose()
    {
        // 关闭时调用
        // 用于：解绑事件、清理资源
    }
}",
                        Explanation = "OnInit 只调用一次，OnOpen 每次打开都调用。"
                    },
                    new()
                    {
                        Title = "动画钩子",
                        Code = @"public class AnimatedPanel : UIPanel
{
    protected override void OnWillShow()
    {
        // 显示动画开始前
        // 用于：播放音效、准备数据
    }

    protected override void OnDidShow()
    {
        // 显示动画完成后
        // 用于：启动逻辑、设置焦点
    }

    protected override void OnWillHide()
    {
        // 隐藏动画开始前
        // 用于：保存状态
    }

    protected override void OnDidHide()
    {
        // 隐藏动画完成后
        // 用于：清理临时数据
    }
}",
                        Explanation = "动画钩子在动画前后触发，适合处理动画相关逻辑。"
                    },
                    new()
                    {
                        Title = "焦点钩子",
                        Code = @"public class StackPanel : UIPanel
{
    protected override void OnFocus()
    {
        // 成为栈顶面板时调用
        base.OnFocus();  // 触发 PanelFocusEvent
    }

    protected override void OnBlur()
    {
        // 失去栈顶位置时调用
        base.OnBlur();   // 触发 PanelBlurEvent
    }

    protected override void OnResume()
    {
        // 从栈中恢复时调用（Pop 后）
        base.OnResume(); // 触发 PanelResumeEvent
    }
}",
                        Explanation = "焦点钩子配合堆栈系统使用，自动管理面板焦点状态。"
                    },
                    new()
                    {
                        Title = "生命周期顺序",
                        Code = @"// 首次打开：
// OnInit → OnOpen → OnWillShow → [动画] → OnShow → OnDidShow

// 再次打开（已缓存）：
// OnOpen → OnWillShow → [动画] → OnShow → OnDidShow

// 隐藏：
// OnWillHide → [动画] → OnHide → OnDidHide

// 关闭：
// OnWillHide → [动画] → OnHide → OnDidHide → OnClose

// 栈操作：
// Push: 前一面板 OnBlur → 新面板 OnFocus
// Pop: 弹出面板 OnBlur → 前一面板 OnResume + OnFocus",
                        Explanation = "生命周期钩子按顺序调用，异常不会中断后续钩子。"
                    }
                }
            };
        }
    }
}
#endif
