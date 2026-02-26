#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class SaveKitDocData
    {
        /// <summary>
        /// 获取所有 SaveKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                SaveKitDocBasic.CreateSection(),
                SaveKitDocAutoSave.CreateSection(),
                SaveKitDocFileFormat.CreateSection(),
                SaveKitDocMigration.CreateSection(),
                SaveKitDocEncryption.CreateSection(),
                SaveKitDocArchitecture.CreateSection()
            };
        }
    }
}
#endif
