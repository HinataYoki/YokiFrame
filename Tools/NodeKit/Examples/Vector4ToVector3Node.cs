using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Vector4 To Vector3")]
    [NodeTint(120, 120, 120)]
    public class Vector4ToVector3Node : Node
    {
        [Input] public Vector4 input;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
