#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 文档数据入口
    /// </summary>
    internal static class PoolKitDocData
    {
        /// <summary>
        /// 获取所有 PoolKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                PoolKitDocSafe.CreateSection(),
                PoolKitDocSimple.CreateSection(),
                PoolKitDocFactory.CreateSection(),
                PoolKitDocCustom.CreateSection(),
                PoolKitDocContainer.CreateSection()
            };
        }
    }
}
#endif
