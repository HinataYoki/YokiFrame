using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Color/Multiply")]
    [NodeTint(160, 110, 110)]
    public class ColorMultiplyNode : Node
    {
        [Input] public Color a = Color.white;
        [Input] public Color b = Color.white;
        [Output] public Color result = Color.white;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) * GetInputValue(nameof(b), b);
        }
    }
}
