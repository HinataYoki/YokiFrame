#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 文档模块入口。
    /// </summary>
    internal static class UIKitDocData
    {
        /// <summary>
        /// 获取 UIKit 的全部文档章节。
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

    internal sealed class UIKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "UIKit",
                Icon = KitIcons.UIKIT,
                Category = "TOOLS",
                Description = "UI 框架工具，覆盖面板生命周期、缓存、动画、异步加载、对话框与导航能力。",
                Keywords = new List<string> { "UI", "面板", "缓存", "动画", "对话框" },
                Sections = UIKitDocData.GetAllSections()
            };
        }
    }
}
#endif
