#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 文档模块入口。
    /// </summary>
    internal static class FsmKitDocData
    {
        /// <summary>
        /// 获取 FsmKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                FsmKitDocBasic.CreateSection(),
                FsmKitDocState.CreateSection(),
                FsmKitDocMessage.CreateSection(),
                FsmKitDocArgs.CreateSection(),
                FsmKitDocHierarchical.CreateSection()
            };
        }
    }

    internal sealed class FsmKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "FsmKit",
                Icon = KitIcons.FSMKIT,
                Category = "CORE KIT",
                Description = "用于流程控制、AI 行为和游戏状态切换的有限状态机工具集。",
                Keywords = new List<string> { "FSM", "状态机", "AI" },
                Sections = FsmKitDocData.GetAllSections()
            };
        }
    }
}
#endif
