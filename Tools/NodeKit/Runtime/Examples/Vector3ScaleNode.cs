using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Vector3/Scale")]
    [NodeTint(80, 140, 140)]
    public class Vector3ScaleNode : Node
    {
        [Input] public Vector3 a = Vector3.one;
        [Input] public Vector3 b = Vector3.one;
        [Output] public Vector3 result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return Vector3.Scale(GetInputValue(nameof(a), a), GetInputValue(nameof(b), b));
        }
    }
}
