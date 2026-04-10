#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 文档模块入口。
    /// </summary>
    internal static class CodeGenKitDocData
    {
        /// <summary>
        /// 获取 CodeGenKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                CodeGenKitDocConcept.CreateSection(),
                CodeGenKitDocGenerate.CreateSection(),
                CodeGenKitDocTypes.CreateSection(),
                CodeGenKitDocMembers.CreateSection(),
                CodeGenKitDocAttributes.CreateSection()
            };
        }
    }

    internal sealed class CodeGenKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "CodeGenKit",
                Icon = KitIcons.CODEGEN,
                Category = "CORE KIT",
                Description = "面向编辑器自动化与代码生成流程的结构化代码生成工具集。",
                Keywords = new List<string> { "代码生成", "Generate", "模板" },
                Sections = CodeGenKitDocData.GetAllSections()
            };
        }
    }
}
#endif
