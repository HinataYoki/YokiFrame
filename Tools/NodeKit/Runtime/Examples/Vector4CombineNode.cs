using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector4/Combine")]
    [NodeTint(80, 120, 150)]
    public class Vector4CombineNode : Node
    {
        [Input] public float x;
        [Input] public float y;
        [Input] public float z;
        [Input] public float w;
        [Output] public Vector4 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return new Vector4(
                GetInputValue(nameof(x), x),
                GetInputValue(nameof(y), y),
                GetInputValue(nameof(z), z),
                GetInputValue(nameof(w), w));
        }
    }
}
