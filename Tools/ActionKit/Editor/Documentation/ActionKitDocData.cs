#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 文档模块入口。
    /// </summary>
    internal static class ActionKitDocData
    {
        /// <summary>
        /// 获取 ActionKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                ActionKitDocBasic.CreateSection(),
                ActionKitDocSequence.CreateSection(),
                ActionKitDocRepeat.CreateSection(),
                ActionKitDocTask.CreateSection(),
                ActionKitDocAsync.CreateSection(),
                ActionKitDocController.CreateSection(),
                ActionKitDocCancel.CreateSection()
            };
        }
    }

    internal sealed class ActionKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "ActionKit",
                Icon = KitIcons.ACTIONKIT,
                Category = "TOOLS",
                Description = "轻量级行为编排系统，支持延迟、回调、序列、并行与重复等流程控制。",
                Keywords = new List<string> { "行为", "序列", "延迟", "回调" },
                Sections = ActionKitDocData.GetAllSections()
            };
        }
    }
}
#endif
