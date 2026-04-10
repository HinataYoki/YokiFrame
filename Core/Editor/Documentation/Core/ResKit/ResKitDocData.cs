#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 文档模块入口。
    /// </summary>
    internal static class ResKitDocData
    {
        /// <summary>
        /// 获取 ResKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            var sections = new List<DocSection>
            {
                ResKitDocSync.CreateSection(),
                ResKitDocAsync.CreateSection(),
                ResKitDocCustomLoader.CreateSection(),
                ResKitDocRawFile.CreateSection(),
                ResKitDocRawFileInterface.CreateSection(),
                ResKitDocYooAssetOverview.CreateSection(),
                ResKitDocYooAssetEditor.CreateSection(),
                ResKitDocYooAssetOffline.CreateSection(),
                ResKitDocYooAssetHost.CreateSection(),
                ResKitDocYooAssetUpdate.CreateSection(),
                ResKitDocYooAssetUsage.CreateSection(),
                ResKitDocAllAssetsAndSubAssets.CreateSection()
            };

            sections.AddRange(ResKitDocYooAssetComplete.GetAllSections());
            return sections;
        }
    }

    internal sealed class ResKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "ResKit",
                Icon = KitIcons.RESKIT,
                Category = "CORE KIT",
                Description = "统一资源加载工具，覆盖同步、异步、原始文件、场景以及 YooAsset 集成等能力。",
                Keywords = new List<string> { "资源", "异步", "引用计数", "YooAsset" },
                Sections = ResKitDocData.GetAllSections()
            };
        }
    }
}
#endif
