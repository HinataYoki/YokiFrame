#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 向文档中心注册 TableKit 文档模块。
    /// </summary>
    internal sealed class TableKitDocumentationProvider : IDocumentationModuleProvider
    {
        /// <summary>
        /// 获取 TableKit 文档模块列表。
        /// </summary>
        /// <returns>TableKit 文档模块枚举。</returns>
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "TableKit",
                Icon = KitIcons.TABLEKIT,
                Category = "TOOLS",
                Description = "Luban-based table workflow for editor configuration, preview, and code generation.",
                Keywords = new List<string> { "Table", "Luban", "Excel", "Data" },
                PluginLinks = new List<PluginLink>
                {
                    new PluginLink { Name = "Luban（必需）", Url = "https://github.com/focus-creative-games/luban" },
                },
                Sections = TableKitDocData.GetAllSections()
            };
        }
    }
}
#endif
