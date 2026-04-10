using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Debug")]
    [NodeTint(150, 100, 100)]
    public class DebugNode : Node
    {
        [Input] public object value;

        public void Execute()
        {
            var v = GetInputValue<object>(nameof(value));
            Debug.Log($"[NodeKit Debug] Value: {v}");
        }
    }
}
