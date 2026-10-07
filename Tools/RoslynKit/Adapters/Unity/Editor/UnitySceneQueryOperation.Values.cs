#if UNITY_EDITOR
using System;
using System.Globalization;
using UnityEngine;

namespace YokiFrame
{
    internal sealed partial class UnitySceneQueryOperation
    {
        /// <summary>把字段值格式化成稳定文本；不支持的复杂类型返回 false。</summary>
        /// <param name="value">字段值。</param>
        /// <param name="text">文本。</param>
        /// <returns>可输出时返回 true。</returns>
        private static bool TryFormatValue(object value, out string text)
        {
            text = string.Empty;
            switch (value)
            {
                case null:
                    text = "null";
                    return true;
                case string stringValue:
                    text = stringValue.Length <= MAX_STRING_CHARS ? stringValue : stringValue.Substring(0, MAX_STRING_CHARS);
                    return true;
                case bool boolValue:
                    text = boolValue ? "true" : "false";
                    return true;
                case Enum enumValue:
                    text = enumValue.ToString();
                    return true;
                case Vector3 vector3:
                    text = Format(vector3.x) + "," + Format(vector3.y) + "," + Format(vector3.z);
                    return true;
                case Vector2 vector2:
                    text = Format(vector2.x) + "," + Format(vector2.y);
                    return true;
                case Color color:
                    text = Format(color.r) + "," + Format(color.g) + "," + Format(color.b) + "," + Format(color.a);
                    return true;
                case UnityEngine.Object unityObject:
                    text = unityObject == null ? "null" : unityObject.name;
                    return true;
                case int intValue:
                    text = intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case long longValue:
                    text = longValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case float floatValue:
                    text = Format(floatValue);
                    return true;
                case double doubleValue:
                    text = doubleValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
