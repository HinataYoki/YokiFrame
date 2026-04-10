namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/String To Bool")]
    [NodeTint(120, 120, 120)]
    public class StringToBoolNode : Node
    {
        [Input] public string input;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            var value = GetInputValue(nameof(input), input);
            return bool.TryParse(value, out bool parsed) && parsed;
        }
    }
}
