using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Normalize")]
    [NodeTint(80, 140, 140)]
    public class Vector3NormalizeNode : Node
    {
        [Input] public Vector3 input;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).normalized;
        }
    }
}
