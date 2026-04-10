using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Color/Split")]
    [NodeTint(160, 110, 110)]
    public class ColorSplitNode : Node
    {
        [Input] public Color input = Color.white;
        [Output] public float r;
        [Output] public float g;
        [Output] public float b;
        [Output] public float a;

        public override object GetValue(NodePort port)
        {
            var value = GetInputValue(nameof(input), input);
            if (port.FieldName == nameof(r)) return value.r;
            if (port.FieldName == nameof(g)) return value.g;
            if (port.FieldName == nameof(b)) return value.b;
            if (port.FieldName == nameof(a)) return value.a;
            return null;
        }
    }
}
