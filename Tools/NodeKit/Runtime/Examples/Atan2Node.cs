using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Atan2")]
    [NodeTint(100, 100, 150)]
    public class Atan2Node : Node
    {
        [Input] public float y;
        [Input] public float x = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.Atan2(GetInputValue(nameof(y), y), GetInputValue(nameof(x), x));
        }
    }
}
