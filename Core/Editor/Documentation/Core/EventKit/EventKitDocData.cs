#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 文档数据入口
    /// </summary>
    internal static class EventKitDocData
    {
        /// <summary>
        /// 获取所有 EventKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                EventKitDocEnum.CreateSection(),
                EventKitDocType.CreateSection(),
                EventKitDocString.CreateSection(),
                EventKitDocEasyEvent.CreateSection(),
                EventKitDocChannel.CreateSection(),
                EventKitDocAdvanced.CreateSection()
            };
        }
    }
}
#endif
