using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Log")]
    [NodeTint(100, 100, 150)]
    public class LogNode : Node
    {
        [Input] public float f = 1f;
        [Input] public float p = 10f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            float value = Mathf.Max(GetInputValue(nameof(f), f), Mathf.Epsilon);
            float power = Mathf.Max(GetInputValue(nameof(p), p), Mathf.Epsilon);
            return Mathf.Log(value, power);
        }
    }
}
