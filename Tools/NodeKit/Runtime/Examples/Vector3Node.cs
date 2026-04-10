using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Vector3")]
    [NodeTint(90, 130, 180)]
    public class Vector3Node : Node
    {
        [Output] public Vector3 value = Vector3.zero;

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(value) ? value : null;
        }
    }
}
