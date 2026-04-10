using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Object")]
    [NodeTint(120, 120, 120)]
    public class ObjectNode : Node
    {
        [Output(ShowBackingValue.Always)] public Object value;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(value)) return null;
            return value;
        }
    }
}
