using System.Globalization;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/String To Int")]
    [NodeTint(120, 120, 120)]
    public class StringToIntNode : Node
    {
        [Input] public string input;
        [Output] public int result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;
        }
    }
}
