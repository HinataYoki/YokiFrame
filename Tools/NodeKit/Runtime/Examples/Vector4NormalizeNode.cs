using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Normalize")]
    [NodeTint(80, 120, 150)]
    public class Vector4NormalizeNode : Node
    {
        [Input] public Vector4 input;
        [Output] public Vector4 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).normalized;
        }
    }
}
