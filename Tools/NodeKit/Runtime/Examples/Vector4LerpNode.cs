using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Lerp")]
    [NodeTint(80, 120, 150)]
    public class Vector4LerpNode : Node
    {
        [Input] public Vector4 a;
        [Input] public Vector4 b = Vector4.one;
        [Input] public float t;
        [Output] public Vector4 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector4.Lerp(
                GetInputValue(nameof(a), a),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(t), t));
        }
    }
}
