#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 文档模块入口。
    /// </summary>
    internal static class AudioKitDocData
    {
        /// <summary>
        /// 获取 AudioKit 的全部文档章节。
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

    internal sealed class AudioKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "AudioKit",
                Icon = KitIcons.AUDIOKIT,
                Category = "TOOLS",
                Description = "音频管理工具，覆盖声道、3D 播放、资源加载以及可选 FMOD 集成。",
                Keywords = new List<string> { "音频", "BGM", "音效", "3D", "FMOD" },
                Sections = AudioKitDocData.GetAllSections()
            };
        }
    }
}
#endif
