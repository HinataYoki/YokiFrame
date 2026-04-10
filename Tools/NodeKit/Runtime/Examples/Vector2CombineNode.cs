using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector2/Combine")]
    [NodeTint(80, 120, 150)]
    public class Vector2CombineNode : Node
    {
        [Input] public float x;
        [Input] public float y;
        [Output] public Vector2 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return new Vector2(GetInputValue(nameof(x), x), GetInputValue(nameof(y), y));
        }
    }
}
