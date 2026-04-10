namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Values/Float")]
    [NodeTint(90, 130, 180)]
    public class FloatNode : Node
    {
        [Output] public float value = 1f;

        public override object GetValue(NodePort port)
        {
            return port.FieldName == nameof(value) ? value : null;
        }
    }
}
