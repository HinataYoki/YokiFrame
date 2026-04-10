using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Scale")]
    [NodeTint(80, 120, 150)]
    public class Vector2ScaleNode : Node
    {
        [Input] public Vector2 a = Vector2.one;
        [Input] public Vector2 b = Vector2.one;
        [Output] public Vector2 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector2.Scale(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
