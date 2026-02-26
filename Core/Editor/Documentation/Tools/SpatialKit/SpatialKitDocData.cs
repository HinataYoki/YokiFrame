#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SpatialKit 文档数据入口
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
}
#endif
