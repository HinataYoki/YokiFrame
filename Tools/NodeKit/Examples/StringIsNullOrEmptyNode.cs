namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Is Null Or Empty")]
    [NodeTint(180, 130, 90)]
    public class StringIsNullOrEmptyNode : Node
    {
        [Input] public string input;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return string.IsNullOrEmpty(GetInputValue(nameof(input), input));
        }
    }
}
