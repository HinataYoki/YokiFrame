using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Vector2")]
    [NodeTint(80, 120, 150)]
    public class Vector2Node : Node
    {
        [Output(ShowBackingValue.Always)] public Vector2 value = Vector2.zero;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(value)) return null;
            return value;
        }
    }
}
