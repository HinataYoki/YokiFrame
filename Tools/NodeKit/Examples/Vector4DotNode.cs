using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Dot")]
    [NodeTint(80, 120, 150)]
    public class Vector4DotNode : Node
    {
        [Input] public Vector4 a;
        [Input] public Vector4 b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector4.Dot(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
