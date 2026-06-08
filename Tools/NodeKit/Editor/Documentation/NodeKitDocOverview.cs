#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// NodeKit 概览文档
    /// </summary>
    internal static class NodeKitDocOverview
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "概览",
                Description = "NodeKit 是基于 Unity GraphView 的节点编辑器框架，用于创建可视化节点图（行为树、对话系统、技能系统等）。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "创建节点图",
                        Code = @"// 1. 创建继承 NodeGraph 的 ScriptableObject
[CreateAssetMenu(menuName = ""MyGame/MyGraph"")]
public class MyGraph : NodeGraph { }

// 2. 在 Project 窗口中右键 > Create > MyGame > MyGraph
// 3. 双击资产打开 Node Graph Editor

// 4. 运行时执行
var runner = GetComponent<NodeGraphRunner>();
runner.Graph = myGraph;
runner.Run();",
                        Explanation = "节点图作为 ScriptableObject 资产保存，支持引用、复制、版本控制。双击直接在编辑器中编辑。"
                    },
                    new()
                    {
                        Title = "核心架构",
                        Code = @"// Node (节点) - ScriptableObject 子资产
// NodeGraph (图)  - 包含节点列表的 ScriptableObject
// NodePort (端口) - 节点的输入/输出连接点

// 声明式端口
public class MyNode : Node
{
    [Input] public float inputValue;      // 输入端口
    [Output] public float outputValue;    // 输出端口

    public override object GetValue(NodePort port)
    {
        // 获取输入值
        float input = GetInputValue<float>(nameof(inputValue));
        // 计算并返回输出值
        return input * 2f;
    }
}",
                        Explanation = "使用 [Input]/[Output] 属性标记端口，框架自动扫描并创建连接。GetValue() 定义节点的计算逻辑。"
                    }
                }
            };
        }
    }
}
#endif
