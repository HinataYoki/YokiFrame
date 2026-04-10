using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Inverse Lerp")]
    [NodeTint(100, 100, 150)]
    public class InverseLerpNode : Node
    {
        [Input] public float a;
        [Input] public float b = 1f;
        [Input] public float value;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.InverseLerp(
                GetInputValue(nameof(a), a),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(value), value));
        }
    }
}
