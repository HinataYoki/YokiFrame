#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SpatialKit 文档模块入口。
    /// </summary>
    internal static class SpatialKitDocData
    {
        internal static List<DocSection> GetAllSections() => new()
        {
            SpatialKitDocOverview.CreateSection(),
            SpatialKitDocQuery.CreateSection(),
            SpatialKitDocGizmos.CreateSection()
        };
    }

    internal sealed class SpatialKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "SpatialKit",
                Icon = KitIcons.SPATIALKIT,
                Category = "TOOLS",
                Description = "空间索引工具集，包含空间哈希网格、四叉树与八叉树实现。",
                Keywords = new List<string> { "空间", "四叉树", "八叉树", "查询" },
                Sections = SpatialKitDocData.GetAllSections()
            };
        }
    }
}
#endif
