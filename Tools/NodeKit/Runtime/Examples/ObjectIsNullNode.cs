using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Logic/Object Is Null")]
    [NodeTint(150, 120, 90)]
    public class ObjectIsNullNode : Node
    {
        [Input] public Object input;
        [Output] public bool result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return GetInputValue(nameof(input), input) == null;
        }
    }
}
