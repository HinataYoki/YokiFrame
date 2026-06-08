namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/String")]
    [NodeTint(90, 130, 180)]
    public class StringNode : Node
    {
        [Output] public string value = "Hello NodeKit";

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(value) ? value : null;
        }
    }
}
