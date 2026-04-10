using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Lerp")]
    [NodeTint(80, 140, 140)]
    public class Vector3LerpNode : Node
    {
        [Input] public Vector3 a;
        [Input] public Vector3 b = Vector3.one;
        [Input] public float t;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector3.Lerp(
                GetInputValue(nameof(a), a),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(t), t));
        }
    }
}
