namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Contains")]
    [NodeTint(180, 130, 90)]
    public class StringContainsNode : Node
    {
        [Input] public string text;
        [Input] public string value;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return (GetInputValue(nameof(text), text) ?? string.Empty)
                .Contains(GetInputValue(nameof(value), value) ?? string.Empty);
        }
    }
}
