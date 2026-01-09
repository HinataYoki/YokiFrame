#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 文档数据入口
    /// </summary>
    internal static class AudioKitDocData
    {
        /// <summary>
        /// 获取所有 AudioKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                AudioKitDocBasic.CreateSection(),
                AudioKitDoc3D.CreateSection(),
                AudioKitDocHandle.CreateSection(),
                AudioKitDocChannel.CreateSection(),
                AudioKitDocResource.CreateSection(),
                AudioKitDocConfig.CreateSection(),
                AudioKitDocFmod.CreateSection()
            };
        }
    }
}
#endif
