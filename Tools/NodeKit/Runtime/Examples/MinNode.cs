using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Min")]
    [NodeTint(100, 100, 150)]
    public class MinNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Min(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
