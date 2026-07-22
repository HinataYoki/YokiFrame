#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>验证 UIKit 只读命令不携带选择器或变更参数。</summary>
    internal static class UIKitPayloadValidator
    {
        private const int PANEL_NAME_BIT = 1 << 0;
        private const int PREFAB_FOLDER_BIT = 1 << 1;
        private const int SCRIPT_FOLDER_BIT = 1 << 2;
        private const int SCRIPT_NAMESPACE_BIT = 1 << 3;
        private const int ASSEMBLY_NAME_BIT = 1 << 4;
        private const int CODE_TEMPLATE_BIT = 1 << 5;
        private const int EXPECTED_CONTEXT_REVISION_BIT = 1 << 6;
        private const int TARGET_GLOBAL_OBJECT_ID_BIT = 1 << 7;
        private const int ALL_PANEL_FIELDS = (1 << 6) - 1;

        /// <summary>只接受空白 payload 或仅含空白的空 JSON 对象。</summary>
        /// <param name="payloadJson">命令 payload。</param>
        internal static void RequireEmptyObject(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson)) return;
            int index = 0;
            SkipWhitespace(payloadJson, ref index);
            if (!TryConsume(payloadJson, ref index, '{')) ThrowInvalidPayload();
            SkipWhitespace(payloadJson, ref index);
            if (!TryConsume(payloadJson, ref index, '}')) ThrowInvalidPayload();
            SkipWhitespace(payloadJson, ref index);
            if (index != payloadJson.Length) ThrowInvalidPayload();
        }

        /// <summary>要求 Panel 生成 payload 恰好包含六个已登记字符串字段。</summary>
        /// <param name="payloadJson">Workbench 或 CLI 提交的命令 payload。</param>
        internal static void RequirePanelGenerationRequest(string payloadJson)
        {
            if (!TryReadFields(payloadJson, true))
                throw new ArgumentException(
                    "UIKit panel generation payload must contain exactly panelName, prefabFolder, "
                    + "scriptFolder, scriptNamespace, assemblyName and codeTemplate as JSON strings; "
                    + "expectedContextRevision and targetGlobalObjectId are optional context fields.");
        }

        /// <summary>验证 Bind 选择操作只携带可选上下文字段或空对象。</summary>
        /// <param name="payloadJson">Workbench 或 CLI 提交的选择上下文 payload。</param>
        internal static void RequireSelectionContext(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson)) return;
            if (!TryReadFields(payloadJson, false))
            {
                throw new ArgumentException(
                    "UIKit selection payload may only contain expectedContextRevision and targetGlobalObjectId.");
            }
        }

        /// <summary>扫描完整顶层对象，拒绝未知、缺失、重复字段及非字符串值。</summary>
        private static bool TryReadFields(string json, bool requirePanelFields)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            int index = 0;
            int fields = 0;
            SkipWhitespace(json, ref index);
            if (!TryConsume(json, ref index, '{')) return false;
            SkipWhitespace(json, ref index);
            while (index < json.Length && json[index] != '}')
            {
                if (!TryReadFieldName(json, ref index, out string fieldName)) return false;
                int fieldBit = GetPanelFieldBit(fieldName);
                if (fieldBit == 0
                    || (fields & fieldBit) != 0
                    || (!requirePanelFields && (fieldBit & ALL_PANEL_FIELDS) != 0)) return false;
                fields |= fieldBit;
                SkipWhitespace(json, ref index);
                if (!TryConsume(json, ref index, ':')) return false;
                SkipWhitespace(json, ref index);
                bool validValue = fieldBit == EXPECTED_CONTEXT_REVISION_BIT
                    ? TrySkipJsonInteger(json, ref index)
                    : TrySkipJsonString(json, ref index);
                if (!validValue) return false;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}') break;
                if (!TryConsume(json, ref index, ',')) return false;
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}') return false;
            }

            if (!TryConsume(json, ref index, '}')) return false;
            SkipWhitespace(json, ref index);
            return index == json.Length
                && (!requirePanelFields || (fields & ALL_PANEL_FIELDS) == ALL_PANEL_FIELDS);
        }

        /// <summary>读取不含转义的固定字段名，防止等价转义绕过字段目录。</summary>
        private static bool TryReadFieldName(string json, ref int index, out string fieldName)
        {
            fieldName = string.Empty;
            if (!TryConsume(json, ref index, '"')) return false;
            int start = index;
            while (index < json.Length)
            {
                char current = json[index++];
                if (current == '"')
                {
                    fieldName = json.Substring(start, index - start - 1);
                    return true;
                }

                if (current == '\\' || current < ' ') return false;
            }

            return false;
        }

        /// <summary>跳过一个语法完整的 JSON 字符串值，不在协议边界复制业务内容。</summary>
        private static bool TrySkipJsonString(string json, ref int index)
        {
            if (!TryConsume(json, ref index, '"')) return false;
            while (index < json.Length)
            {
                char current = json[index++];
                if (current == '"') return true;
                if (current < ' ') return false;
                if (current != '\\') continue;
                if (index >= json.Length) return false;
                char escaped = json[index++];
                if (escaped == 'u')
                {
                    if (!TrySkipHexQuad(json, ref index)) return false;
                }
                else if (escaped != '"' && escaped != '\\' && escaped != '/'
                    && escaped != 'b' && escaped != 'f' && escaped != 'n'
                    && escaped != 'r' && escaped != 't')
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>验证并跳过 JSON unicode 转义中的四个十六进制字符。</summary>
        private static bool TrySkipHexQuad(string json, ref int index)
        {
            if (index + 4 > json.Length) return false;
            for (var offset = 0; offset < 4; offset++)
            {
                char current = json[index++];
                bool isHex = current >= '0' && current <= '9'
                    || current >= 'a' && current <= 'f'
                    || current >= 'A' && current <= 'F';
                if (!isHex) return false;
            }

            return true;
        }

        /// <summary>把固定 Panel 字段名映射为去重位标识。</summary>
        private static int GetPanelFieldBit(string fieldName)
        {
            if (fieldName == "panelName") return PANEL_NAME_BIT;
            if (fieldName == "prefabFolder") return PREFAB_FOLDER_BIT;
            if (fieldName == "scriptFolder") return SCRIPT_FOLDER_BIT;
            if (fieldName == "scriptNamespace") return SCRIPT_NAMESPACE_BIT;
            if (fieldName == "assemblyName") return ASSEMBLY_NAME_BIT;
            if (fieldName == "codeTemplate") return CODE_TEMPLATE_BIT;
            if (fieldName == "expectedContextRevision") return EXPECTED_CONTEXT_REVISION_BIT;
            return fieldName == "targetGlobalObjectId" ? TARGET_GLOBAL_OBJECT_ID_BIT : 0;
        }

        /// <summary>跳过一个 JSON 整数值，拒绝小数、指数和空数字。</summary>
        /// <param name="json">待扫描 JSON。</param>
        /// <param name="index">当前值起始位置。</param>
        /// <returns>当前位置为合法整数并已前移时返回 true。</returns>
        private static bool TrySkipJsonInteger(string json, ref int index)
        {
            int start = index;
            if (index < json.Length && json[index] == '-') index++;
            int digits = index;
            while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
            return index > digits && index > start;
        }

        /// <summary>跳过 JSON 允许的空白字符。</summary>
        /// <param name="json">待扫描 JSON。</param>
        /// <param name="index">当前扫描位置。</param>
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char current = json[index];
                if (current != ' ' && current != '\t' && current != '\r' && current != '\n') return;
                index++;
            }
        }

        /// <summary>尝试消费一个固定 JSON 结构字符。</summary>
        /// <param name="json">待扫描 JSON。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <param name="expected">期望字符。</param>
        /// <returns>当前位置匹配并已前移时返回 true。</returns>
        private static bool TryConsume(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected) return false;
            index++;
            return true;
        }

        /// <summary>抛出稳定的 UIKit 只读 payload 契约错误。</summary>
        private static void ThrowInvalidPayload()
        {
            throw new ArgumentException("UIKit read-only command payload must be blank or an empty JSON object.");
        }
    }
}
#endif
