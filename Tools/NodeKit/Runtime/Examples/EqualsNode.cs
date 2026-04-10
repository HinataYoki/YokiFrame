namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Equals")]
    [NodeTint(150, 120, 90)]
    public class EqualsNode : Node
    {
        [Input] public object a;
        [Input] public object b;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Equals(GetInputValue<object>(nameof(a), a), GetInputValue<object>(nameof(b), b));
        }
    }
}
