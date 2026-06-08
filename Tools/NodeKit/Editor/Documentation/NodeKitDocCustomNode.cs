#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// NodeKit 自定义节点文档
    /// </summary>
    internal static class NodeKitDocCustomNode
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "自定义节点",
                Description = "继承 Node 基类并使用属性标记端口，即可创建自定义节点。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础节点",
                        Code = @"[CreateNodeMenu(""MyGame/Add"")]
[NodeTint(100, 150, 200)]
[NodeWidth(250)]
public class AddNode : Node
{
    [Input] public float a;
    [Input] public float b;
    [Output] public float result;

    public override object GetValue(NodePort port)
    {
        if (port.FieldName == nameof(result))
        {
            float va = GetInputValue<float>(nameof(a));
            float vb = GetInputValue<float>(nameof(b));
            return va + vb;
        }
        return null;
    }
}",
                        Explanation = "CreateNodeMenu 定义在搜索窗口中的路径。NodeTint 设置节点颜色。NodeWidth 设置节点宽度。GetValue 定义输出逻辑。"
                    },
                    new()
                    {
                        Title = "动态端口",
                        Code = @"[CreateNodeMenu(""MyGame/MultiInput"")]
public class MultiInputNode : Node
{
    // 动态端口列表：运行时通过 UI 添加/删除端口
    [Input(DynamicPortList = true)]
    public float[] values;

    public override object GetValue(NodePort port)
    {
        if (port.FieldName == nameof(result))
            return GetInputValues<float>(nameof(values)).Sum();
        return null;
    }
}",
                        Explanation = "设置 DynamicPortList = true，框架自动生成 + / - 按钮和排序功能。"
                    },
                    new()
                    {
                        Title = "端口约束",
                        Code = @"public class StrictNode : Node
{
    // 严格类型匹配：仅允许连接 float 类型
    [Input(TypeConstraint = TypeConstraint.Strict)]
    public float strictInput;

    // 继承匹配：允许 float 的子类型
    [Input(TypeConstraint = TypeConstraint.Inherited)]
    public Component inheritedInput;

    // 多个连接（默认）vs 单个连接
    [Input(ConnectionType = ConnectionType.Multiple)]
    public float multiInput;

    [Input(ConnectionType = ConnectionType.Override)]
    public float singleInput;  // 新连接替换旧连接
}",
                        Explanation = "TypeConstraint 控制端口类型兼容性。ConnectionType 控制端口可接受的连接数。"
                    },
                    new()
                    {
                        Title = "自定义编辑器",
                        Code = @"[CustomNodeEditor(typeof(MyNode))]
public class MyNodeEditor : NodeEditorBase
{
    public override void OnHeaderGUI(VisualElement container)
    {
        // 自定义标题栏
        var label = new Label(Target.name);
        label.style.color = Color.yellow;
        container.Add(label);
    }

    public override void OnBodyGUI(VisualElement container)
    {
        // 调用默认序列化属性绘制
        base.OnBodyGUI(container);

        // 添加自定义 UI
        var button = new Button(() => Debug.Log(""Custom action""))
        {
            text = ""Custom Button""
        };
        container.Add(button);
    }

    public override Color GetTint()
    {
        return new Color(0.2f, 0.6f, 0.2f);
    }
}

[CustomNodeGraphEditor(typeof(MyGraph))]
public class MyGraphEditor : NodeGraphEditorBase
{
    public override string GetNodeMenuName(Type type)
    {
        return $""MyGame/{type.Name}"";
    }
}",
                        Explanation = "通过 CustomNodeEditor/CustomNodeGraphEditor 属性绑定自定义编辑器，继承 NodeEditorBase/NodeGraphEditorBase 扩展。"
                    }
                }
            };
        }
    }
}
#endif
