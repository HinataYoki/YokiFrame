namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Int")]
    [NodeTint(90, 130, 180)]
    public class IntNode : Node
    {
        [Output] public int value = 1;

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(value) ? value : null;
        }
    }
}
