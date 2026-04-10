#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 文档模块入口。
    /// </summary>
    internal static class BuffKitDocData
    {
        /// <summary>
        /// 获取 BuffKit 的全部文档章节。
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

    internal sealed class BuffKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "BuffKit",
                Icon = KitIcons.BUFFKIT,
                Category = "TOOLS",
                Description = "支持叠加、查询、事件、免疫、修饰器与序列化的 Buff 系统。",
                Keywords = new List<string> { "Buff", "Debuff", "Modifier", "RPG" },
                Sections = BuffKitDocData.GetAllSections()
            };
        }
    }
}
#endif
