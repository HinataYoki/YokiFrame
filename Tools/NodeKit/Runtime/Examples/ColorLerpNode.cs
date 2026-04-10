using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Color/Lerp")]
    [NodeTint(160, 110, 110)]
    public class ColorLerpNode : Node
    {
        [Input] public Color a = Color.white;
        [Input] public Color b = Color.black;
        [Input] public float t;
        [Output] public Color result = Color.white;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Color.Lerp(
                GetInputValue(nameof(a), a),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(t), t));
        }
    }
}
