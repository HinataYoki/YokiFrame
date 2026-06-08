namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Subtract")]
    [NodeTint(100, 100, 150)]
    public class SubtractNode : Node
    {
        [Input] public float a;
        [Input] public float b;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) - GetInputValue(nameof(b), b);
        }
    }
}
