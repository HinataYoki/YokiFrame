using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Deg2Rad")]
    [NodeTint(100, 100, 150)]
    public class Deg2RadNode : Node
    {
        [Input] public float degrees;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(degrees), degrees) * Mathf.Deg2Rad;
        }
    }
}
