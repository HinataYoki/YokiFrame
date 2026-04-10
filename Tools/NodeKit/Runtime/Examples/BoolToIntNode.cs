namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Bool To Int")]
    [NodeTint(120, 120, 120)]
    public class BoolToIntNode : Node
    {
        [Input] public bool input;
        [Output] public int result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input) ? 1 : 0;
        }
    }
}
