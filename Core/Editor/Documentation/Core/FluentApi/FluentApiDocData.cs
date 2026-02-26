#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class FluentApiDocData
    {
        /// <summary>
        /// 获取所有 FluentApi 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                FluentApiDocObject.CreateSection(),
                FluentApiDocString.CreateSection(),
                FluentApiDocTransform.CreateSection(),
                FluentApiDocVector.CreateSection(),
                FluentApiDocColor.CreateSection(),
                FluentApiDocNumeric.CreateSection()
            };
        }
    }
}
#endif
