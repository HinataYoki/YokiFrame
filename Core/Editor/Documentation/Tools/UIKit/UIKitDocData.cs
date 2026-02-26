#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class UIKitDocData
    {
        /// <summary>
        /// 获取所有 UIKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                UIKitDocBasic.CreateSection(),
                UIKitDocAnimation.CreateSection(),
                UIKitDocLifecycle.CreateSection(),
                UIKitDocStack.CreateSection(),
                UIKitDocCache.CreateSection(),
                UIKitDocFocus.CreateSection(),
                UIKitDocSelectableGroup.CreateSection(),
                UIKitDocGamepad.CreateSection(),
                UIKitDocDialog.CreateSection(),
                UIKitDocModal.CreateSection(),
                UIKitDocCanvas.CreateSection(),
                UIKitDocLayout.CreateSection(),
                UIKitDocBind.CreateSection(),
                UIKitDocCodeGenExtension.CreateSection(),
                UIKitDocCustomPanel.CreateSection(),
                UIKitDocEditorTools.CreateSection()
            };
        }
    }
}
#endif
