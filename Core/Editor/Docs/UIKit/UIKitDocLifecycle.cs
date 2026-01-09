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
                Description = "UIKit 提供丰富的生命周期钩子，支持动画前后回调、焦点管理等。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整生命周期",
                        Code = @"public class MyPanel : UIPanel
{
    protected override void OnInit(IUIData data = null)
    {
        // 面板首次创建时调用，只调用一次
    }

    protected override void OnOpen(IUIData data = null)
    {
        // 每次打开面板时调用
    }

    protected override void OnWillShow()
    {
        // 显示动画开始前调用
    }

    protected override void OnDidShow()
    {
        // 显示动画完成后调用
    }

    protected override void OnWillHide()
    {
        // 隐藏动画开始前调用
    }

    protected override void OnDidHide()
    {
        // 隐藏动画完成后调用
    }

    protected override void OnClose()
    {
        // 面板关闭时调用
    }
}",
                        Explanation = "生命周期钩子按顺序调用，异常不会中断后续钩子。"
                    },
                    new()
                    {
                        Title = "焦点管理钩子",
                        Code = @"public class MyPanel : UIPanel
{
    protected override void OnFocus()
    {
        // 面板成为栈顶时调用
    }

    protected override void OnBlur()
    {
        // 面板失去栈顶位置时调用
    }

    protected override void OnResume()
    {
        // 面板从栈中恢复时调用（Pop 后）
    }
}",
                        Explanation = "焦点钩子配合堆栈系统使用，自动管理面板焦点状态。"
                    }
                }
            };
        }
    }
}
#endif
