#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class LocalizationKitDocData
    {
        /// <summary>
        /// 获取所有 LocalizationKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                LocalizationKitDocQuickStart.CreateSection(),
                LocalizationKitDocLanguage.CreateSection(),
                LocalizationKitDocPlural.CreateSection(),
                LocalizationKitDocBind.CreateSection(),
                LocalizationKitDocAsync.CreateSection(),
                LocalizationKitDocProvider.CreateSection(),
                LocalizationKitDocFormat.CreateSection(),
                LocalizationKitDocSaveKit.CreateSection(),
                LocalizationKitDocBestPractice.CreateSection()
            };
        }
    }
}
#endif
