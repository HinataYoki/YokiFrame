#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 文档模块的数据入口。
    /// </summary>
    internal static class ArchitectureDocData
    {
        /// <summary>
        /// 返回 Architecture 模块的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                ArchitectureDocOverview.CreateSection(),
                ArchitectureDocCreate.CreateSection(),
                ArchitectureDocService.CreateSection(),
                ArchitectureDocModel.CreateSection(),
                ArchitectureDocUsage.CreateSection(),
                ArchitectureDocSaveKit.CreateSection()
            };
        }
    }

    /// <summary>
    /// Architecture 文档模块提供器。
    /// </summary>
    internal sealed class ArchitectureDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "架构",
                Icon = KitIcons.ARCHITECTURE,
                Category = "CORE",
                Description = "YokiFrame 核心架构系统：服务注册、依赖注入与模块化组织。",
                Keywords = new List<string> { "架构", "DI", "IoC", "服务", "模块" },
                Sections = ArchitectureDocData.GetAllSections()
            };
        }
    }
}
#endif
