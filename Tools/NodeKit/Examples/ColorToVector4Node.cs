using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Color To Vector4")]
    [NodeTint(120, 120, 120)]
    public class ColorToVector4Node : Node
    {
        [Input] public Color input = Color.white;
        [Output] public Vector4 result = Vector4.one;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var color = GetInputValue(nameof(input), input);
            return new Vector4(color.r, color.g, color.b, color.a);
        }
    }
}
