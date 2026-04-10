using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Vector2 To Vector3")]
    [NodeTint(120, 120, 120)]
    public class Vector2ToVector3Node : Node
    {
        [Input] public Vector2 input;
        [Input] public float z;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return new Vector3(value.x, value.y, GetInputValue(nameof(z), z));
        }
    }
}
