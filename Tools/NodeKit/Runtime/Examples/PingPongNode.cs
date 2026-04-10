using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Ping Pong")]
    [NodeTint(100, 100, 150)]
    public class PingPongNode : Node
    {
        [Input] public float t;
        [Input] public float length = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.PingPong(GetInputValue(nameof(t), t), GetInputValue(nameof(length), length));
        }
    }
}
