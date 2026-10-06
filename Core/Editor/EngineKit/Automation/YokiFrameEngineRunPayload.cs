#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// 自动化操作的共享 payload 读取：只接受字符串 id 字段。
    /// </summary>
    internal static class YokiFrameEngineRunPayload
    {
        /// <summary>读取单个字符串字段。</summary>
        /// <param name="payloadJson">payload JSON。</param>
        /// <param name="field">字段名。</param>
        /// <param name="value">字段值。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>读取成功时返回 true。</returns>
        public static bool TryReadId(string payloadJson, string field, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "payload requires a " + field + " field.";
                return false;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(payloadJson))
                {
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        error = "payload must be a JSON object.";
                        return false;
                    }

                    if (!root.TryGetProperty(field, out JsonElement element)
                        || element.ValueKind != JsonValueKind.String)
                    {
                        error = "payload requires a string " + field + " field.";
                        return false;
                    }

                    value = (element.GetString() ?? string.Empty).Trim();
                    if (value.Length == 0)
                    {
                        error = "payload " + field + " is empty.";
                        return false;
                    }

                    return true;
                }
            }
            catch (JsonException exception)
            {
                error = "payload is not valid JSON: " + exception.Message;
                return false;
            }
        }
    }
}
#endif
