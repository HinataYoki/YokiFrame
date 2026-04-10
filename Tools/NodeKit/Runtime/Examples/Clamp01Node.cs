using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Clamp01")]
    [NodeTint(100, 100, 150)]
    public class Clamp01Node : Node
    {
        [Input] public float input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Clamp01(GetInputValue(nameof(input), input));
        }
    }
}
