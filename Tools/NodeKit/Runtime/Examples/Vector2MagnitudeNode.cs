using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Magnitude")]
    [NodeTint(80, 120, 150)]
    public class Vector2MagnitudeNode : Node
    {
        [Input] public Vector2 input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).magnitude;
        }
    }
}
