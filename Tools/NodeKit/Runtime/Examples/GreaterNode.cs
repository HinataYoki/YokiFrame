namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Greater")]
    [NodeTint(150, 120, 90)]
    public class GreaterNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) > GetInputValue(nameof(b), b);
        }
    }
}
