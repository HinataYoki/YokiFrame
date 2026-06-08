using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Magnitude")]
    [NodeTint(80, 120, 150)]
    public class Vector4MagnitudeNode : Node
    {
        [Input] public Vector4 input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).magnitude;
        }
    }
}
