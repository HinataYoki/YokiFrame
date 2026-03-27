using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    /// <summary>
    /// 示例节点图
    /// </summary>
    [CreateAssetMenu(fileName = "ExampleNodeGraph", menuName = "YokiFrame/NodeKit/Example Graph")]
    public class ExampleNodeGraph : NodeGraph { }

    /// <summary>
    /// 示例起始节点
    /// </summary>
    [CreateNodeMenu("Example/Start")]
    [NodeTint(80, 150, 80)]
    public class StartNode : Node
    {
        [Output] public float output;

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(output) ? 1f : null;
        }
    }

    /// <summary>
    /// 示例数学节点
    /// </summary>
    [CreateNodeMenu("Example/Math/Add")]
    [NodeTint(100, 100, 150)]
    public class AddNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) + GetInputValue(nameof(b), b);
        }
    }

    /// <summary>
    /// 示例输出节点
    /// </summary>
    [CreateNodeMenu("Example/Debug")]
    [NodeTint(150, 100, 100)]
    public class DebugNode : Node
    {
        [Input] public object value;

        public void Execute()
        {
            var v = GetInputValue<object>(nameof(value));
            Debug.Log($"[NodeKit Debug] Value: {v}");
        }
    }
}
