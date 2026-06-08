namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Concat")]
    [NodeTint(120, 90, 150)]
    public class ConcatNode : Node
    {
        [Input] public string a;
        [Input] public string b;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return (GetInputValue(nameof(a), a) ?? string.Empty) + (GetInputValue(nameof(b), b) ?? string.Empty);
        }
    }
}
