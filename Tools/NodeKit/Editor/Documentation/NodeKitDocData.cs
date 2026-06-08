#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    internal static class NodeKitDocData
    {
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                NodeKitDocOverview.CreateSection(),
                NodeKitDocCreateGraph.CreateSection(),
                NodeKitDocCustomNode.CreateSection()
            };
        }
    }

    internal sealed class NodeKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "NodeKit",
                Icon = "d_AnimatorController Icon",
                Category = "TOOLS",
                Description = "基于 Unity GraphView 的可视化节点编辑器框架，用于创建行为树、对话系统、技能系统等节点图。",
                Keywords = new List<string> { "node", "graph", "可视化", "节点", "编辑器", "graphview" },
                Sections = NodeKitDocData.GetAllSections()
            };
        }
    }
}
#endif
