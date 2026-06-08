using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Cross")]
    [NodeTint(80, 140, 140)]
    public class Vector3CrossNode : Node
    {
        [Input] public Vector3 a;
        [Input] public Vector3 b;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector3.Cross(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
