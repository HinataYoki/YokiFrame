using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Add")]
    [NodeTint(80, 140, 140)]
    public class Vector3AddNode : Node
    {
        [Input] public Vector3 a;
        [Input] public Vector3 b;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) + GetInputValue(nameof(b), b);
        }
    }
}
