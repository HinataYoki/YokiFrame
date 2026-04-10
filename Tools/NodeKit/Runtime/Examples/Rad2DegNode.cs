using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Rad2Deg")]
    [NodeTint(100, 100, 150)]
    public class Rad2DegNode : Node
    {
        [Input] public float radians;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(radians), radians) * Mathf.Rad2Deg;
        }
    }
}
