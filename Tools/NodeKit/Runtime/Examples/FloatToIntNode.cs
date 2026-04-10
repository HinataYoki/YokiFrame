using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Float To Int")]
    [NodeTint(120, 120, 120)]
    public class FloatToIntNode : Node
    {
        [Input] public float input;
        [Output] public int result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Mathf.RoundToInt(GetInputValue(nameof(input), input));
        }
    }
}
