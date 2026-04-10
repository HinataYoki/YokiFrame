using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Color/Combine")]
    [NodeTint(160, 110, 110)]
    public class ColorCombineNode : Node
    {
        [Input] public float r = 1f;
        [Input] public float g = 1f;
        [Input] public float b = 1f;
        [Input] public float a = 1f;
        [Output] public Color result = Color.white;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return new Color(
                GetInputValue(nameof(r), r),
                GetInputValue(nameof(g), g),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(a), a));
        }
    }
}
