namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Bool To String")]
    [NodeTint(120, 120, 120)]
    public class BoolToStringNode : Node
    {
        [Input] public bool input;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input).ToString();
        }
    }
}
