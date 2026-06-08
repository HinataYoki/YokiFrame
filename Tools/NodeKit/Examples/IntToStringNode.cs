namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Int To String")]
    [NodeTint(120, 120, 120)]
    public class IntToStringNode : Node
    {
        [Input] public int input;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).ToString();
        }
    }
}
