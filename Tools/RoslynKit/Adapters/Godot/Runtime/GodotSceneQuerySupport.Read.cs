#if GODOT && TOOLS
using YokiFrame.Json;

namespace YokiFrame
{
    public static partial class GodotSceneQuerySupport
    {
        /// <summary>解析 payload 对象。空白由调用方按默认值处理；非法 JSON 或非对象直接失败。</summary>
        /// <param name="payloadJson">payload 文本。</param>
        /// <param name="document">成功时的文档，调用方负责释放。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>根元素是对象时返回 true。</returns>
        private static bool TryParseObject(string payloadJson, out JsonDocument document, out string error)
        {
            document = null;
            error = string.Empty;
            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "scene_query payload is not valid JSON: " + exception.Message;
                return false;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            error = "scene_query payload must be a JSON object.";
            document.Dispose();
            document = null;
            return false;
        }

        /// <summary>
        /// 读取 path、includeInactive 和 depth。缺省字段保持调用方给的默认值；
        /// 类型不对时失败，不把非法值夹成默认值。
        /// </summary>
        /// <param name="root">payload 根对象。</param>
        /// <param name="path">请求路径。</param>
        /// <param name="depth">夹取后的深度。</param>
        /// <param name="includeInactive">是否包含不可见节点。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>字段都可用时返回 true。</returns>
        private static bool TryReadQueryFields(
            JsonElement root,
            ref string path,
            ref int depth,
            ref bool includeInactive,
            out string error)
        {
            error = string.Empty;
            if (!TryReadPath(root, ref path, out error)
                || !TryReadIncludeInactive(root, ref includeInactive, out error)
                || !TryReadDepth(root, ref depth, out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>读取 path。缺省或 null 保持空字符串；非字符串失败。</summary>
        /// <param name="root">payload 根对象。</param>
        /// <param name="path">请求路径。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>字段可用或缺失时返回 true。</returns>
        private static bool TryReadPath(JsonElement root, ref string path, out string error)
        {
            error = string.Empty;
            if (!root.TryGetProperty("path", out JsonElement pathValue) || pathValue.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (pathValue.ValueKind != JsonValueKind.String)
            {
                error = "scene_query path must be a string.";
                return false;
            }

            path = pathValue.GetString() ?? string.Empty;
            return true;
        }

        /// <summary>读取 includeInactive。缺省或 null 保持调用方默认值；非布尔失败。</summary>
        /// <param name="root">payload 根对象。</param>
        /// <param name="includeInactive">是否包含不可见节点。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>字段可用或缺失时返回 true。</returns>
        private static bool TryReadIncludeInactive(JsonElement root, ref bool includeInactive, out string error)
        {
            error = string.Empty;
            if (!root.TryGetProperty("includeInactive", out JsonElement includeValue) || includeValue.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (includeValue.ValueKind != JsonValueKind.True && includeValue.ValueKind != JsonValueKind.False)
            {
                error = "scene_query includeInactive must be a boolean.";
                return false;
            }

            includeInactive = includeValue.ValueKind == JsonValueKind.True;
            return true;
        }

        /// <summary>读取 depth 并夹到 0 与最大深度之间。缺省或 null 保持默认深度；非整数失败。</summary>
        /// <param name="root">payload 根对象。</param>
        /// <param name="depth">夹取后的深度。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>字段可用或缺失时返回 true。</returns>
        private static bool TryReadDepth(JsonElement root, ref int depth, out string error)
        {
            error = string.Empty;
            if (!root.TryGetProperty("depth", out JsonElement depthValue) || depthValue.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (!depthValue.TryGetInt32(out int requested))
            {
                error = "scene_query depth must be an integer.";
                return false;
            }

            depth = requested < 0 ? 0 : requested > MAX_DEPTH ? MAX_DEPTH : requested;
            return true;
        }
    }
}
#endif
