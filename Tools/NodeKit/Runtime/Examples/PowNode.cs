using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Pow")]
    [NodeTint(100, 100, 150)]
    public class PowNode : Node
    {
        [Input] public float f;
        [Input] public float p = 2f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Pow(GetInputValue(nameof(f), f), GetInputValue(nameof(p), p));
        }
    }
}
