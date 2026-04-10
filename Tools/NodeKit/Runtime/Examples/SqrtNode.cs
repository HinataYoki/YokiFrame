using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Sqrt")]
    [NodeTint(100, 100, 150)]
    public class SqrtNode : Node
    {
        [Input] public float input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Sqrt(Mathf.Max(0f, GetInputValue(nameof(input), input)));
        }
    }
}
