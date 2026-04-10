using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Exp")]
    [NodeTint(100, 100, 150)]
    public class ExpNode : Node
    {
        [Input] public float power;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Exp(GetInputValue(nameof(power), power));
        }
    }
}
