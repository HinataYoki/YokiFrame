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
                Description = "UIKit 提供模态面板功能，用于创建需要用户必须处理的对话框。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "设置模态面板",
                        Code = @"// 打开面板后设置为模态
var panel = UIKit.OpenPanel<ConfirmDialog>(UILevel.Pop);
UIKit.SetPanelModal(panel, true);

// 在面板内部设置
protected override void OnOpen(IUIData data = null)
{
    UIKit.SetPanelModal(this, true);
}

// 取消模态状态
UIKit.SetPanelModal(panel, false);",
                        Explanation = "模态面板会在下方创建半透明遮罩，阻断下层 UI 交互。"
                    },
                    new()
                    {
                        Title = "检查模态状态",
                        Code = @"// 检查当前是否有模态面板
if (UIKit.HasModalBlocker())
{
    Debug.Log(""当前有模态对话框"");
    return;
}",
                        Explanation = "HasModalBlocker 可用于判断是否有模态面板正在阻断交互。"
                    }
                }
            };
        }
    }
}
#endif
