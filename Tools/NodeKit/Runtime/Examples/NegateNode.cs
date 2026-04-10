namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Negate")]
    [NodeTint(100, 100, 150)]
    public class NegateNode : Node
    {
        [Input] public float input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return -GetInputValue(nameof(input), input);
        }
    }
}
