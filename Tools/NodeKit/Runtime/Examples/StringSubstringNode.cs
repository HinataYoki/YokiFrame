using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    [CreateNodeMenu("Example/String/Substring")]
    [NodeTint(180, 130, 90)]
    public class StringSubstringNode : Node
    {
        [Input] public string input;
        [Input] public int startIndex;
        [Input] public int length = 1;
        [Output] public string result;

        public override object GetValue(NodePort port)
        {
            if (port.FieldName != nameof(result)) return null;

            string value = GetInputValue(nameof(input), input) ?? string.Empty;
            int start = Mathf.Clamp(GetInputValue(nameof(startIndex), startIndex), 0, value.Length);
            int count = Mathf.Clamp(GetInputValue(nameof(length), length), 0, value.Length - start);
            return value.Substring(start, count);
        }
    }
}
