#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class CodeGenKitDocData
    {
        /// <summary>
        /// 获取所有 CodeGenKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                CodeGenKitDocConcept.CreateSection(),
                CodeGenKitDocGenerate.CreateSection(),
                CodeGenKitDocTypes.CreateSection(),
                CodeGenKitDocMembers.CreateSection(),
                CodeGenKitDocAttributes.CreateSection()
            };
        }
    }
}
#endif
