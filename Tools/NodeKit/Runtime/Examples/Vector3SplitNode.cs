using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Split")]
    [NodeTint(80, 140, 140)]
    public class Vector3SplitNode : Node
    {
        [Input] public Vector3 input;
        [Output] public float x;
        [Output] public float y;
        [Output] public float z;

        public override object GetValue(NodePort port)
        {
            var value = GetInputValue(nameof(input), input);
            if (port.FieldName == nameof(x)) return value.x;
            if (port.FieldName == nameof(y)) return value.y;
            if (port.FieldName == nameof(z)) return value.z;
            return null;
        }
    }
}
