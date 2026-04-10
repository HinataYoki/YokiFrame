using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Clamp")]
    [NodeTint(100, 100, 150)]
    public class ClampNode : Node
    {
        [Input] public float value;
        [Input] public float min = 0f;
        [Input] public float max = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Clamp(
                GetInputValue(nameof(value), value),
                GetInputValue(nameof(min), min),
                GetInputValue(nameof(max), max));
        }
    }
}
