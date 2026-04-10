using System.Globalization;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/String To Float")]
    [NodeTint(120, 120, 120)]
    public class StringToFloatNode : Node
    {
        [Input] public string input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float number) ? number : 0f;
        }
    }
}
