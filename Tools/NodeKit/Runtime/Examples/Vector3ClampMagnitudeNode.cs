using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Clamp Magnitude")]
    [NodeTint(80, 140, 140)]
    public class Vector3ClampMagnitudeNode : Node
    {
        [Input] public Vector3 input;
        [Input] public float maxLength = 1f;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector3.ClampMagnitude(
                GetInputValue(nameof(input), input),
                GetInputValue(nameof(maxLength), maxLength));
        }
    }
}
