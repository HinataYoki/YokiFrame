namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Length")]
    [NodeTint(180, 130, 90)]
    public class StringLengthNode : Node
    {
        [Input] public string input;
        [Output] public int result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return (GetInputValue(nameof(input), input) ?? string.Empty).Length;
        }
    }
}
