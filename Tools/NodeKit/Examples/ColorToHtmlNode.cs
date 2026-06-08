using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/Conversion/Color To Html")]
    [NodeTint(120, 120, 120)]
    public class ColorToHtmlNode : Node
    {
        [Input] public Color input = Color.white;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;
            return "#" + ColorUtility.ToHtmlStringRGBA(GetInputValue(nameof(input), input));
        }
    }
}
