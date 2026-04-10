using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Divide")]
    [NodeTint(100, 100, 150)]
    public class DivideNode : Node
    {
        [Input] public float a = 1f;
        [Input] public float b = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;

            float dividend = GetInputValue(nameof(a), a);
            float divisor = GetInputValue(nameof(b), b);
            return Mathf.Approximately(divisor, 0f) ? 0f : dividend / divisor;
        }
    }
}
