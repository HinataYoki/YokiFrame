using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Vector3 To Vector2")]
    [NodeTint(120, 120, 120)]
    public class Vector3ToVector2Node : Node
    {
        [Input] public Vector3 input;
        [Output] public Vector2 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return new Vector2(value.x, value.y);
        }
    }
}
