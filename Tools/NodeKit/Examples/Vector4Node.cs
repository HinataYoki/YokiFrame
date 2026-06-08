using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Vector4")]
    [NodeTint(80, 120, 150)]
    public class Vector4Node : Node
    {
        [Output(ShowBackingValue.Always)] public Vector4 value = Vector4.zero;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(value)) return null;
            return value;
        }
    }
}
