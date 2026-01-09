#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 文档数据入口
    /// </summary>
    internal static class BuffKitDocData
    {
        /// <summary>
        /// 获取所有 BuffKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                BuffKitDocQuickStart.CreateSection(),
                BuffKitDocStackMode.CreateSection(),
                BuffKitDocQuery.CreateSection(),
                BuffKitDocImmunity.CreateSection(),
                BuffKitDocModifier.CreateSection(),
                BuffKitDocCustom.CreateSection(),
                BuffKitDocEvent.CreateSection(),
                BuffKitDocSerialization.CreateSection(),
                BuffKitDocCharacter.CreateSection()
            };
        }
    }
}
#endif
