using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Repeat")]
    [NodeTint(100, 100, 150)]
    public class RepeatNode : Node
    {
        [Input] public float t;
        [Input] public float length = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Repeat(GetInputValue(nameof(t), t), GetInputValue(nameof(length), length));
        }
    }
}
