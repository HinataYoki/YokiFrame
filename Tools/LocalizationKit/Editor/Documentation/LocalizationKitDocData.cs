#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 文档模块入口。
    /// </summary>
    internal static class LocalizationKitDocData
    {
        /// <summary>
        /// 获取 LocalizationKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                LocalizationKitDocQuickStart.CreateSection(),
                LocalizationKitDocLanguage.CreateSection(),
                LocalizationKitDocBind.CreateSection(),
                LocalizationKitDocCustomBinder.CreateSection(),
                LocalizationKitDocPlural.CreateSection(),
                LocalizationKitDocAsync.CreateSection(),
                LocalizationKitDocProvider.CreateSection(),
                LocalizationKitDocFormat.CreateSection(),
                LocalizationKitDocSaveKit.CreateSection(),
                LocalizationKitDocBestPractice.CreateSection()
            };
        }
    }

    internal sealed class LocalizationKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "LocalizationKit",
                Icon = KitIcons.LOCALIZATIONKIT,
                Category = "TOOLS",
                Description = "本地化系统，支持语言切换、格式化、复数规则、UI 绑定（文本/图片/音频等自定义 Binder）以及异步 Provider。",
                Keywords = new List<string> { "本地化", "i18n", "语言", "文本", "binder", "绑定", "扩展" },
                Sections = LocalizationKitDocData.GetAllSections()
            };
        }
    }
}
#endif
