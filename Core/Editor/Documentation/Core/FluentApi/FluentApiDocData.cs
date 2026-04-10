#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi 文档模块入口。
    /// </summary>
    internal static class FluentApiDocData
    {
        /// <summary>
        /// 获取 FluentApi 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                FluentApiDocObject.CreateSection(),
                FluentApiDocString.CreateSection(),
                FluentApiDocTransform.CreateSection(),
                FluentApiDocVector.CreateSection(),
                FluentApiDocColor.CreateSection(),
                FluentApiDocNumeric.CreateSection()
            };
        }
    }

    internal sealed class FluentApiDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "FluentApi",
                Icon = KitIcons.FLUENTAPI,
                Category = "CORE KIT",
                Description = "面向对象、字符串、Transform、向量等类型的链式辅助扩展集合。",
                Keywords = new List<string> { "扩展", "Fluent", "链式语法" },
                Sections = FluentApiDocData.GetAllSections()
            };
        }
    }
}
#endif
