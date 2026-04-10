using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Max")]
    [NodeTint(100, 100, 150)]
    public class MaxNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Max(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
