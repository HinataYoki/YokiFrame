#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 文档数据入口
    /// </summary>
    internal static class ActionKitDocData
    {
        /// <summary>
        /// 获取所有 ActionKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                ActionKitDocBasic.CreateSection(),
                ActionKitDocSequence.CreateSection(),
                ActionKitDocRepeat.CreateSection(),
                ActionKitDocTask.CreateSection(),
                ActionKitDocAsync.CreateSection(),
                ActionKitDocController.CreateSection(),
                ActionKitDocCancel.CreateSection()
            };
        }
    }
}
#endif
