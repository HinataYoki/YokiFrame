#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 文档模块入口。
    /// </summary>
    internal static class InputKitDocData
    {
        /// <summary>
        /// 获取 InputKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                InputKitDocQuickStart.CreateSection(),
                InputKitDocInit.CreateSection(),
                InputKitDocInput.CreateSection(),
                InputKitDocRebind.CreateSection(),
                InputKitDocBuffer.CreateSection(),
                InputKitDocCombo.CreateSection(),
                InputKitDocContext.CreateSection(),
                InputKitDocTouch.CreateSection(),
                InputKitDocHaptic.CreateSection(),
                InputKitDocDebug.CreateSection()
            };
        }
    }

    internal sealed class InputKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "InputKit",
                Icon = KitIcons.INPUTKIT,
                Category = "TOOLS",
                Description = "输入工作流工具，覆盖重绑、输入缓冲、连招、上下文、触屏、震动与调试支持。",
                Keywords = new List<string> { "输入", "重绑", "连招", "触屏", "震动" },
                Sections = InputKitDocData.GetAllSections()
            };
        }
    }
}
#endif
