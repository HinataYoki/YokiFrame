namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Select")]
    [NodeTint(150, 120, 90)]
    public class SelectNode : Node
    {
        [Input] public bool condition;
        [Input] public object ifTrue;
        [Input] public object ifFalse;
        [Output] public object result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;

            bool predicate = GetInputValue(nameof(condition), condition);
            return predicate
                ? GetInputValue<object>(nameof(ifTrue), ifTrue)
                : GetInputValue<object>(nameof(ifFalse), ifFalse);
        }
    }
}
