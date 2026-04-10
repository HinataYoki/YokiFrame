using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Dot")]
    [NodeTint(80, 120, 150)]
    public class Vector2DotNode : Node
    {
        [Input] public Vector2 a;
        [Input] public Vector2 b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector2.Dot(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
