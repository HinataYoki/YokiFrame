#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 模态面板文档
    /// </summary>
    internal static class UIKitDocModal
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "模态面板",
                Description = "模态面板会创建半透明遮罩阻断下层 UI 交互，适用于确认对话框、重要提示等必须处理的场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本用法",
                        Code = @"// 打开面板后设置为模态
var panel = UIKit.OpenPanel<ConfirmDialog>(UILevel.Pop);
UIKit.SetPanelModal(panel, true);

// 在面板内部设置（推荐）
protected override void OnOpen(IUIData data = null)
{
    base.OnOpen(data);
    UIKit.SetPanelModal(this, true);
}

protected override void OnClose()
{
    // 关闭时自动移除模态遮罩，无需手动处理
    base.OnClose();
}",
                        Explanation = "模态遮罩会自动创建在面板下方，面板关闭时自动销毁。"
                    },
                    new()
                    {
                        Title = "模态状态查询与控制",
                        Code = @"// 检查是否有模态面板
if (UIKit.HasModalBlocker())
{
    Debug.Log(""当前有模态对话框，无法执行操作"");
    return;
}

// 动态取消模态状态
UIKit.SetPanelModal(panel, false);

// 多层模态面板支持
// 打开多个模态面板时，只有最顶层可交互
// 关闭顶层后，下一层自动恢复交互",
                        Explanation = "模态系统支持多层嵌套，自动管理各层的交互状态。"
                    },
                    new()
                    {
                        Title = "模态遮罩特性",
                        Code = @"// 模态遮罩特性：
// - 颜色：半透明黑色 (0, 0, 0, 0.5f)
// - 位置：自动放置在面板下方
// - 射线：阻断所有下层 UI 的点击事件
// - 交互：禁用下层面板的 CanvasGroup.interactable

// 模态面板的典型使用场景：
// - 确认对话框（删除、退出确认）
// - 重要提示（网络错误、版本更新）
// - 输入对话框（改名、输入密码）
// - 加载遮罩（防止用户操作）",
                        Explanation = "模态遮罩通过 Image + CanvasGroup 实现交互阻断。"
                    }
                }
            };
        }
    }
}
#endif
