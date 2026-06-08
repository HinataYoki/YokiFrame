using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Combine")]
    [NodeTint(80, 140, 140)]
    public class Vector3CombineNode : Node
    {
        [Input] public float x;
        [Input] public float y;
        [Input] public float z;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return new Vector3(
                GetInputValue(nameof(x), x),
                GetInputValue(nameof(y), y),
                GetInputValue(nameof(z), z));
        }
    }
}
