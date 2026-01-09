#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 文档数据入口
    /// </summary>
    internal static class FsmKitDocData
    {
        /// <summary>
        /// 获取所有 FsmKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                FsmKitDocBasic.CreateSection(),
                FsmKitDocState.CreateSection(),
                FsmKitDocMessage.CreateSection(),
                FsmKitDocArgs.CreateSection(),
                FsmKitDocHierarchical.CreateSection()
            };
        }
    }
}
#endif
