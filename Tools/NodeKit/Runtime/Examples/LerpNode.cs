using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Lerp")]
    [NodeTint(100, 100, 150)]
    public class LerpNode : Node
    {
        [Input] public float a;
        [Input] public float b = 1f;
        [Input] public float t;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Lerp(
                GetInputValue(nameof(a), a),
                GetInputValue(nameof(b), b),
                GetInputValue(nameof(t), t));
        }
    }
}
