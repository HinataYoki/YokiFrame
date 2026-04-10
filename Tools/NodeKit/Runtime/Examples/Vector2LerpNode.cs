using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Lerp")]
    [NodeTint(80, 120, 150)]
    public class Vector2LerpNode : Node
    {
        [Input] public Vector2 a;
        [Input] public Vector2 b = Vector2.one;
        [Input] public float t;
        [Output] public Vector2 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector2.Lerp(
                GetInputValue(nameof(a), a),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(t), t));
        }
    }
}
