using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Add")]
    [NodeTint(80, 120, 150)]
    public class Vector4AddNode : Node
    {
        [Input] public Vector4 a;
        [Input] public Vector4 b;
        [Output] public Vector4 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) + GetInputValue(nameof(b), b);
        }
    }
}
