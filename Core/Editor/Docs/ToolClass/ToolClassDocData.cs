#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ToolClass 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class ToolClassDocData
    {
        /// <summary>
        /// 获取所有 ToolClass 文档模块
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
}
#endif
