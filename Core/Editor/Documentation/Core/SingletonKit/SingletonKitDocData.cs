#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SingletonKit 文档模块入口。
    /// </summary>
    internal static class SingletonKitDocData
    {
        /// <summary>
        /// 获取 SingletonKit 的全部文档章节。
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

    internal sealed class SingletonKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "SingletonKit",
                Icon = KitIcons.SINGLETON,
                Category = "CORE KIT",
                Description = "面向普通 C# 类型与 MonoBehaviour 工作流的单例工具集。",
                Keywords = new List<string> { "单例", "全局访问" },
                Sections = SingletonKitDocData.GetAllSections()
            };
        }
    }
}
#endif
