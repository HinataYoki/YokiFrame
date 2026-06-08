using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Move Towards")]
    [NodeTint(80, 140, 140)]
    public class Vector3MoveTowardsNode : Node
    {
        [Input] public Vector3 current;
        [Input] public Vector3 target = Vector3.one;
        [Input] public float maxDistanceDelta = 0.1f;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector3.MoveTowards(
                GetInputValue(nameof(current), current),
                GetInputValue(nameof(target), target),
                GetInputValue(nameof(maxDistanceDelta), maxDistanceDelta));
        }
    }
}
