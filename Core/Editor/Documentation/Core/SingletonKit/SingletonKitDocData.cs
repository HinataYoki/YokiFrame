#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SingletonKit 文档数据入口
    /// </summary>
    internal static class SingletonKitDocData
    {
        /// <summary>
        /// 获取所有 SingletonKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                SingletonKitDocNormal.CreateSection(),
                SingletonKitDocMono.CreateSection(),
                SingletonKitDocPath.CreateSection()
            };
        }
    }
}
#endif
