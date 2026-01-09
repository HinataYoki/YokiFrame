#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class TableKitDocData
    {
        /// <summary>
        /// 获取所有 TableKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                TableKitDocOverview.CreateSection(),
                TableKitDocEditorConfig.CreateSection(),
                TableKitDocRuntime.CreateSection(),
                TableKitDocEditorMode.CreateSection(),
                TableKitDocExternalType.CreateSection(),
                TableKitDocBestPractice.CreateSection()
            };
        }
    }
}
#endif
