#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ToolClass 文档模块入口。
    /// </summary>
    internal static class ToolClassDocData
    {
        /// <summary>
        /// 获取 ToolClass 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                ToolClassDocBindValue.CreateSection(),
                ToolClassDocPooledLinkedList.CreateSection(),
                ToolClassDocSpanSplitter.CreateSection(),
                ToolClassDocFastDictionary.CreateSection()
            };
        }
    }

    internal sealed class ToolClassDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "ToolClass",
                Icon = KitIcons.TOOLCLASS,
                Category = "CORE KIT",
                Description = "包含 BindValue、PooledLinkedList、SpanSplitter、FastDictionary 等高性能基础工具类型。",
                Keywords = new List<string> { "工具类", "Bindable", "字典", "性能" },
                Sections = ToolClassDocData.GetAllSections()
            };
        }
    }
}
#endif
