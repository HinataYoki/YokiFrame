using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Sqr Magnitude")]
    [NodeTint(80, 140, 140)]
    public class Vector3SqrMagnitudeNode : Node
    {
        [Input] public Vector3 input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).sqrMagnitude;
        }
    }
}
