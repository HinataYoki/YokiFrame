#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 文档模块入口。
    /// </summary>
    internal static class SceneKitDocData
    {
        /// <summary>
        /// 获取 SceneKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                SceneKitDocBasic.CreateSection(),
                SceneKitDocTransition.CreateSection(),
                SceneKitDocPreload.CreateSection(),
                SceneKitDocUnload.CreateSection(),
                SceneKitDocQuery.CreateSection(),
                SceneKitDocEvent.CreateSection(),
                SceneKitDocUniTask.CreateSection(),
                SceneKitDocLoader.CreateSection(),
                SceneKitDocEditor.CreateSection(),
                SceneKitDocBestPractice.CreateSection()
            };
        }
    }

    internal sealed class SceneKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "SceneKit",
                Icon = KitIcons.SCENEKIT,
                Category = "TOOLS",
                Description = "场景工作流工具，覆盖加载、卸载、预加载、切换以及自定义加载器集成。",
                Keywords = new List<string> { "场景", "切换", "预加载", "异步" },
                Sections = SceneKitDocData.GetAllSections()
            };
        }
    }
}
#endif
