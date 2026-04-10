using System.Globalization;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Float To String")]
    [NodeTint(120, 120, 120)]
    public class FloatToStringNode : Node
    {
        [Input] public float input;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).ToString(CultureInfo.InvariantCulture);
        }
    }
}
