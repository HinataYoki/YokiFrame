using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Fract")]
    [NodeTint(100, 100, 150)]
    public class FractNode : Node
    {
        [Input] public float input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            float value = GetInputValue(nameof(input), input);
            return value - Mathf.Floor(value);
        }
    }
}
