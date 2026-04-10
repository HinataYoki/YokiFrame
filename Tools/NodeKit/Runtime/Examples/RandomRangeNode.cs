using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Random Range")]
    [NodeTint(100, 100, 150)]
    public class RandomRangeNode : Node
    {
        [Input] public float min;
        [Input] public float max = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Random.Range(GetInputValue(nameof(min), min), GetInputValue(nameof(max), max));
        }
    }
}
