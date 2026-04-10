using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Color")]
    [NodeTint(160, 110, 110)]
    public class ColorNode : Node
    {
        [Output(ShowBackingValue.Always)] public Color value = Color.white;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(value)) return null;
            return value;
        }
    }
}
