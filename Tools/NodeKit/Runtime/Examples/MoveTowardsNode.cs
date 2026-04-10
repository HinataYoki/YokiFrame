using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Move Towards")]
    [NodeTint(100, 100, 150)]
    public class MoveTowardsNode : Node
    {
        [Input] public float current;
        [Input] public float target = 1f;
        [Input] public float maxDelta = 0.1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.MoveTowards(
                GetInputValue(nameof(current), current),
                GetInputValue(nameof(target), target),
                GetInputValue(nameof(maxDelta), maxDelta));
        }
    }
}
