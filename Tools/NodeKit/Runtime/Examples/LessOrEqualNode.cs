namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Less Or Equal")]
    [NodeTint(150, 120, 90)]
    public class LessOrEqualNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) <= GetInputValue(nameof(b), b);
        }
    }
}
