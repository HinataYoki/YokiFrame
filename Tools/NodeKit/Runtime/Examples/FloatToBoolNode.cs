using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Float To Bool")]
    [NodeTint(120, 120, 120)]
    public class FloatToBoolNode : Node
    {
        [Input] public float input;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return !Mathf.Approximately(GetInputValue(nameof(input), input), 0f);
        }
    }
}
