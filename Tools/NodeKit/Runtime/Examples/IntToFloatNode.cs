namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Int To Float")]
    [NodeTint(120, 120, 120)]
    public class IntToFloatNode : Node
    {
        [Input] public int input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return (float)GetInputValue(nameof(input), input);
        }
    }
}
