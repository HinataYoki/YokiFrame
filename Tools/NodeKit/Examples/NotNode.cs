namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Not")]
    [NodeTint(150, 120, 90)]
    public class NotNode : Node
    {
        [Input] public bool input;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return !GetInputValue(nameof(input), input);
        }
    }
}
