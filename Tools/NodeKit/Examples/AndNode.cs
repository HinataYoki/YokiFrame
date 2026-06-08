namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/And")]
    [NodeTint(150, 120, 90)]
    public class AndNode : Node
    {
        [Input] public bool a;
        [Input] public bool b;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) && GetInputValue(nameof(b), b);
        }
    }
}
