namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Math/Multiply")]
    [NodeTint(100, 100, 150)]
    public class MultiplyNode : Node
    {
        [Input] public float a = 1f;
        [Input] public float b = 1f;
        [Output] public float result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(a), a) * GetInputValue(nameof(b), b);
        }
    }
}
