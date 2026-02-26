#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 文档数据入口
    /// </summary>
    internal static class ArchitectureDocData
    {
        /// <summary>
        /// 获取所有 Architecture 文档章节
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
}
#endif
