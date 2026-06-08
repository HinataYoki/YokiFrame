namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Start")]
    [NodeTint(80, 150, 80)]
    public class StartNode : Node
    {
        [Output] public float output;

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(output) ? 1f : null;
        }
    }
}
