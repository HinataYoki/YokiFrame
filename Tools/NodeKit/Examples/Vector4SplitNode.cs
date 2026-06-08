using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Split")]
    [NodeTint(80, 120, 150)]
    public class Vector4SplitNode : Node
    {
        [Input] public Vector4 input;
        [Output] public float x;
        [Output] public float y;
        [Output] public float z;
        [Output] public float w;

        public override object GetValue(NodePort port)
        {
            var value = GetInputValue(nameof(input), input);
            if (port.FieldName == nameof(x)) return value.x;
            if (port.FieldName == nameof(y)) return value.y;
            if (port.FieldName == nameof(z)) return value.z;
            if (port.FieldName == nameof(w)) return value.w;
            return null;
        }
    }
}
