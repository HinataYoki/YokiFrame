namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/To Upper")]
    [NodeTint(180, 130, 90)]
    public class StringToUpperNode : Node
    {
        [Input] public string input;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return (GetInputValue(nameof(input), input) ?? string.Empty).ToUpperInvariant();
        }
    }
}
