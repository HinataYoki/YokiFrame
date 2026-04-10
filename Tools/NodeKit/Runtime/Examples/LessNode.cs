namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Less")]
    [NodeTint(150, 120, 90)]
    public class LessNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) < GetInputValue(nameof(b), b);
        }
    }
}
