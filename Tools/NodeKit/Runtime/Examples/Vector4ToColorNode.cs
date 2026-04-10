using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Vector4 To Color")]
    [NodeTint(120, 120, 120)]
    public class Vector4ToColorNode : Node
    {
        [Input] public Vector4 input = Vector4.one;
        [Output] public Color result = Color.white;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return new Color(value.x, value.y, value.z, value.w);
        }
    }
}
