namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Bool To Float")]
    [NodeTint(120, 120, 120)]
    public class BoolToFloatNode : Node
    {
        [Input] public bool input;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input) ? 1f : 0f;
        }
    }
}
