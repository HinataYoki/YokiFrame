namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Int To Bool")]
    [NodeTint(120, 120, 120)]
    public class IntToBoolNode : Node
    {
        [Input] public int input;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input) != 0;
        }
    }
}
