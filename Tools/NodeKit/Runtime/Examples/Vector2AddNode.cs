using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Add")]
    [NodeTint(80, 120, 150)]
    public class Vector2AddNode : Node
    {
        [Input] public Vector2 a;
        [Input] public Vector2 b;
        [Output] public Vector2 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) + GetInputValue(nameof(b), b);
        }
    }
}
