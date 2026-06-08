using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Random Range Int")]
    [NodeTint(100, 100, 150)]
    public class RandomRangeIntNode : Node
    {
        [Input] public int min;
        [Input] public int max = 10;
        [Output] public int result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Random.Range(GetInputValue(nameof(min), min), GetInputValue(nameof(max), max));
        }
    }
}
