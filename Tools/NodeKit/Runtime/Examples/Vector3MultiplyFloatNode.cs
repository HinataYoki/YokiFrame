using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Multiply Float")]
    [NodeTint(80, 140, 140)]
    public class Vector3MultiplyFloatNode : Node
    {
        [Input] public Vector3 input = Vector3.one;
        [Input] public float scalar = 1f;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input) * GetInputValue(nameof(scalar), scalar);
        }
    }
}
