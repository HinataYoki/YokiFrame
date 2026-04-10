using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Normalize")]
    [NodeTint(80, 120, 150)]
    public class Vector2NormalizeNode : Node
    {
        [Input] public Vector2 input;
        [Output] public Vector2 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).normalized;
        }
    }
}
