using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Smooth Step")]
    [NodeTint(100, 100, 150)]
    public class SmoothStepNode : Node
    {
        [Input] public float from;
        [Input] public float to = 1f;
        [Input] public float t;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.SmoothStep(
                GetInputValue(nameof(from), from),
                GetInputValue(nameof(to), to),
                GetInputValue(nameof(t), t));
        }
    }
}
