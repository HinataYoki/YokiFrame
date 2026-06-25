using System;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// CommandBridge 协议解析用的零依赖 JSON 辅助类。
    /// 只处理 .yokiframe 命令文件使用的扁平对象结构。
    /// 这不是通用 JSON 解析器，只服务 { "kit":"...", "action":"...", "payload":{...} } 这类协议 envelope。
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 从简单扁平 JSON 对象中提取字符串字段。
        /// 示例：ExtractString("{\"kit\":\"FsmKit\"}", "kit") → "FsmKit"。
        /// 字段不存在时返回 null。
        /// </summary>
        public static string ExtractString(string json, string fieldName)
        {
            var search = $"\"{fieldName}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx += search.Length;
            // 跳过空白字符和冒号。
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length) return null;

            if (json[idx] == '"')
            {
                // 字符串值。
                idx++;
                int endIdx = json.IndexOf('"', idx);
                if (endIdx < 0) return null;
                return json.Substring(idx, endIdx - idx);
            }

            return null;
        }

        /// <summary>
        /// 将嵌套对象或数组字段提取为原始 JSON 字符串。
        /// 示例：ExtractRaw("{\"payload\":{\"fsmName\":\"x\"}}", "payload") → "{\"fsmName\":\"x\"}"。
        /// 字段不存在时返回 null。
        /// </summary>
        public static string ExtractRaw(string json, string fieldName)
        {
            var search = $"\"{fieldName}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length) return null;

            int start = idx;
            if (json[idx] == '{')
                return ExtractBraceBlock(json, idx, '{', '}');
            if (json[idx] == '[')
                return ExtractBraceBlock(json, idx, '[', ']');
            if (json[idx] == '"')
            {
                idx++;
                int endIdx = json.IndexOf('"', idx);
                if (endIdx < 0) return null;
                return json.Substring(start, endIdx - start + 1);
            }

            return null;
        }

        /// <summary>
        /// 从简单 JSON 对象中提取 int 字段，支持数字值和字符串数字值。
        /// </summary>
        public static bool TryExtractInt(string json, string fieldName, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
                return false;

            var stringValue = ExtractString(json, fieldName);
            if (stringValue != null)
                return int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

            var primitiveValue = ExtractPrimitive(json, fieldName);
            return primitiveValue != null &&
                   int.TryParse(primitiveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 从简单 JSON 对象中提取 bool 字段，支持 true/false 和字符串 true/false。
        /// </summary>
        public static bool TryExtractBool(string json, string fieldName, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
                return false;

            var stringValue = ExtractString(json, fieldName);
            if (stringValue != null)
                return bool.TryParse(stringValue, out value);

            var primitiveValue = ExtractPrimitive(json, fieldName);
            if (primitiveValue == null)
                return false;

            if (string.Equals(primitiveValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(primitiveValue, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        private static string ExtractPrimitive(string json, string fieldName)
        {
            var search = $"\"{fieldName}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length || json[idx] == '"' || json[idx] == '{' || json[idx] == '[')
                return null;

            var start = idx;
            while (idx < json.Length && json[idx] != ',' && json[idx] != '}' && json[idx] != ']' &&
                   json[idx] != ' ' && json[idx] != '\t' && json[idx] != '\r' && json[idx] != '\n')
            {
                idx++;
            }

            return idx > start ? json.Substring(start, idx - start) : null;
        }

        private static string ExtractBraceBlock(string json, int start, char open, char close)
        {
            int depth = 0;
            bool inString = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                if (inString) continue;
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        /// <summary>
        /// 根据 requestId 和结果数据构建最小成功响应 JSON。
        /// </summary>
        public static string BuildResponse(string requestId, string kit, string action, string status, string dataJson)
        {
            return BuildResponse(requestId, kit, action, status, dataJson, "base");
        }

        /// <summary>
        /// 根据 requestId、engineId 和结果数据构建标准响应 JSON。
        /// </summary>
        public static string BuildResponse(string requestId, string kit, string action, string status, string dataJson, string engineId)
        {
            var sb = new StringBuilder(256);
            var completedAtUtc = DateTime.UtcNow.ToString("O");
            sb.Append("{\"protocolVersion\":2,\"requestId\":\"");
            sb.Append(EscapeString(requestId ?? string.Empty));
            sb.Append("\",\"engineId\":\"");
            sb.Append(EscapeString(string.IsNullOrEmpty(engineId) ? "base" : engineId));
            sb.Append("\",\"status\":\"");
            sb.Append(EscapeString(status));
            sb.Append("\",\"kit\":\"");
            sb.Append(EscapeString(kit ?? string.Empty));
            sb.Append("\",\"action\":\"");
            sb.Append(EscapeString(action ?? string.Empty));
            sb.Append("_response\",\"timestamp\":\"");
            sb.Append(completedAtUtc);
            sb.Append("\",\"completedAtUtc\":\"");
            sb.Append(completedAtUtc);
            sb.Append("\"");
            if (!string.IsNullOrEmpty(dataJson))
            {
                sb.Append(",\"data\":");
                sb.Append(dataJson);
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 构建标准错误响应 JSON。
        /// </summary>
        public static string BuildError(string requestId, string kit, string action, string errorMessage)
        {
            return BuildError(requestId, kit, action, errorMessage, "base", "CommandError", false);
        }

        /// <summary>
        /// 构建带错误码、宿主标识和可恢复标记的标准错误响应 JSON。
        /// </summary>
        public static string BuildError(string requestId, string kit, string action, string errorMessage, string engineId, string errorCode, bool recoverable)
        {
            var sb = new StringBuilder(256);
            var completedAtUtc = DateTime.UtcNow.ToString("O");
            var safeMessage = errorMessage ?? "Unknown error";
            sb.Append("{\"protocolVersion\":2,\"requestId\":\"");
            sb.Append(EscapeString(requestId ?? string.Empty));
            sb.Append("\",\"engineId\":\"");
            sb.Append(EscapeString(string.IsNullOrEmpty(engineId) ? "base" : engineId));
            sb.Append("\",\"status\":\"error\",\"kit\":\"");
            sb.Append(EscapeString(kit ?? string.Empty));
            sb.Append("\",\"action\":\"");
            sb.Append(EscapeString(action ?? string.Empty));
            sb.Append("_response\",\"timestamp\":\"");
            sb.Append(completedAtUtc);
            sb.Append("\",\"completedAtUtc\":\"");
            sb.Append(completedAtUtc);
            sb.Append("\",\"error\":{\"code\":\"");
            sb.Append(EscapeString(string.IsNullOrEmpty(errorCode) ? "CommandError" : errorCode));
            sb.Append("\",\"message\":\"");
            sb.Append(EscapeString(safeMessage));
            sb.Append("\",\"recoverable\":");
            sb.Append(recoverable ? "true" : "false");
            sb.Append("},\"errorMessage\":\"");
            sb.Append(EscapeString(safeMessage));
            sb.Append("\"}");
            return sb.ToString();
        }

        /// <summary>
        /// 转义字符串，生成可嵌入 JSON 字符串字面量的内容。
        /// </summary>
        public static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            var sb = new StringBuilder(s.Length + 8);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
