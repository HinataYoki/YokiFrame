namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Bool")]
    [NodeTint(90, 130, 180)]
    public class BoolNode : Node
    {
        [Output] public bool value = true;

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(value) ? value : null;
        }
    }
}
