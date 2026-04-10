using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Dot")]
    [NodeTint(80, 140, 140)]
    public class Vector3DotNode : Node
    {
        [Input] public Vector3 a;
        [Input] public Vector3 b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector3.Dot(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
