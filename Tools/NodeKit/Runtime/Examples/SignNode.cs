using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Sign")]
    [NodeTint(100, 100, 150)]
    public class SignNode : Node
    {
        [Input] public float input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Sign(GetInputValue(nameof(input), input));
        }
    }
}
