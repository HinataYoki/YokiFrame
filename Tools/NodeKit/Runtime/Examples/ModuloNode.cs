using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Modulo")]
    [NodeTint(100, 100, 150)]
    public class ModuloNode : Node
    {
        [Input] public float a = 1f;
        [Input] public float b = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            float divisor = GetInputValue(nameof(b), b);
            if (Mathf.Approximately(divisor, 0f))
                return 0f;
            return GetInputValue(nameof(a), a) % divisor;
        }
    }
}
