using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Split")]
    [NodeTint(80, 120, 150)]
    public class Vector2SplitNode : Node
    {
        [Input] public Vector2 input;
        [Output] public float x;
        [Output] public float y;

        public override object GetValue(NodePort port)
        {
            var value = GetInputValue(nameof(input), input);
            if (port.FieldName == nameof(x)) return value.x;
            if (port.FieldName == nameof(y)) return value.y;
            return null;
        }
    }
}
