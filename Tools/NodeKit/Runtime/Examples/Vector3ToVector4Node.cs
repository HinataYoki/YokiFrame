using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Vector3 To Vector4")]
    [NodeTint(120, 120, 120)]
    public class Vector3ToVector4Node : Node
    {
        [Input] public Vector3 input;
        [Input] public float w;
        [Output] public Vector4 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return new Vector4(value.x, value.y, value.z, GetInputValue(nameof(w), w));
        }
    }
}
