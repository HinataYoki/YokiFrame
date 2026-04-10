#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 文档模块入口。
    /// </summary>
    internal static class SaveKitDocData
    {
        /// <summary>
        /// 获取 SaveKit 的全部文档章节。
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

    internal sealed class SaveKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "SaveKit",
                Icon = KitIcons.SAVEKIT,
                Category = "TOOLS",
                Description = "存档系统，支持槽位、迁移、加密、自动保存以及异步友好的工作流。",
                Keywords = new List<string> { "存档", "持久化", "加密", "迁移" },
                Sections = SaveKitDocData.GetAllSections()
            };
        }
    }
}
#endif
