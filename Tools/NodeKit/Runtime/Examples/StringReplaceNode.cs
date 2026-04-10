namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Replace")]
    [NodeTint(180, 130, 90)]
    public class StringReplaceNode : Node
    {
        [Input] public string input;
        [Input] public string oldValue;
        [Input] public string newValue;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;

            string source = GetInputValue(nameof(input), input) ?? string.Empty;
            string oldText = GetInputValue(nameof(oldValue), oldValue) ?? string.Empty;
            string newText = GetInputValue(nameof(newValue), newValue) ?? string.Empty;
            return source.Replace(oldText, newText);
        }
    }
}
