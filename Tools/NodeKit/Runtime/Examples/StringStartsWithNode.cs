namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Starts With")]
    [NodeTint(180, 130, 90)]
    public class StringStartsWithNode : Node
    {
        [Input] public string input;
        [Input] public string value;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return (GetInputValue(nameof(input), input) ?? string.Empty)
                .StartsWith(GetInputValue(nameof(value), value) ?? string.Empty);
        }
    }
}
