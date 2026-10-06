#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// Godot 场景修改的共享支持：payload 解析、节点创建与变换应用。
    /// </summary>
    /// <remarks>
    /// 操作名与 Unity 侧一致（create/delete/setActive/setTransform/save），
    /// 差异只在引擎细节：Godot 用 <c>nodeType</c> 指定类名（Unity 用 components），删除是延迟的 QueueFree。
    /// </remarks>
    public static class GodotSceneMutateSupport
    {
        /// <summary>创建操作的 op 名。</summary>
        public const string OP_CREATE = "create";

        /// <summary>删除操作的 op 名。</summary>
        public const string OP_DELETE = "delete";

        /// <summary>激活操作的 op 名。</summary>
        public const string OP_SET_ACTIVE = "setActive";

        /// <summary>变换操作的 op 名。</summary>
        public const string OP_SET_TRANSFORM = "setTransform";

        /// <summary>保存操作的 op 名。</summary>
        public const string OP_SAVE = "save";

        /// <summary>解析 payload 的 op 字段。</summary>
        /// <param name="payloadJson">payload。</param>
        /// <param name="op">操作名。</param>
        /// <param name="document">已解析文档；调用方负责释放。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>解析成功时返回 true。</returns>
        public static bool TryReadPayload(
            string payloadJson,
            out string op,
            out JsonDocument document,
            out string error)
        {
            op = string.Empty;
            document = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "scene_mutate requires an op field.";
                return false;
            }

            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "scene_mutate payload is not valid JSON: " + exception.Message;
                return false;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                document = null;
                error = "scene_mutate payload must be a JSON object.";
                return false;
            }

            op = ReadString(document.RootElement, "op");
            if (op.Length == 0)
            {
                document.Dispose();
                document = null;
                error = "scene_mutate requires a non-empty op field.";
                return false;
            }

            return true;
        }

        /// <summary>读取字符串字段。</summary>
        /// <param name="element">对象。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段值；缺失时为空。</returns>
        public static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty).Trim()
                : string.Empty;
        }

        /// <summary>按类名实例化节点；类名不存在时返回 null。</summary>
        /// <param name="typeName">Godot 类名，例如 Node3D。</param>
        /// <returns>新节点；失败时为 null。</returns>
        public static Node CreateNode(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || !ClassDB.ClassExists(typeName))
            {
                return null;
            }

            Variant instance = ClassDB.Instantiate(typeName);
            return instance.VariantType == Variant.Type.Nil ? null : instance.As<Node>();
        }

        /// <summary>读取三元/二元浮点数组。</summary>
        /// <param name="root">payload 根。</param>
        /// <param name="name">字段名。</param>
        /// <param name="minLength">允许的最小长度。</param>
        /// <param name="values">读取结果。</param>
        /// <returns>读取成功时返回 true。</returns>
        public static bool TryReadVector(JsonElement root, string name, int minLength, out float[] values)
        {
            values = Array.Empty<float>();
            if (!root.TryGetProperty(name, out JsonElement element)
                || element.ValueKind != JsonValueKind.Array
                || element.GetArrayLength() < minLength)
            {
                return false;
            }

            int length = element.GetArrayLength();
            var parsed = new float[length];
            for (var index = 0; index < length; index++)
            {
                if (!double.TryParse(
                        element[index].ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double value))
                {
                    return false;
                }

                parsed[index] = (float)value;
            }

            values = parsed;
            return true;
        }

        /// <summary>读取布尔字段。</summary>
        /// <param name="root">payload 根。</param>
        /// <param name="name">字段名。</param>
        /// <param name="value">读取结果。</param>
        /// <returns>读取成功时返回 true。</returns>
        public static bool TryReadBoolean(JsonElement root, string name, out bool value)
        {
            value = false;
            if (!root.TryGetProperty(name, out JsonElement element))
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                value = element.ValueKind == JsonValueKind.True;
                return true;
            }

            return false;
        }

        /// <summary>应用节点可见性/处理模式。</summary>
        /// <param name="node">目标节点。</param>
        /// <param name="active">目标状态。</param>
        /// <returns>是否通过可见性实现（否则用 ProcessMode）。</returns>
        public static bool ApplyActive(Node node, bool active)
        {
            if (node is CanvasItem canvasItem)
            {
                canvasItem.Visible = active;
                return true;
            }

            if (node is Node3D node3D)
            {
                node3D.Visible = active;
                return true;
            }

            node.ProcessMode = active ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled;
            return false;
        }

        /// <summary>应用 2D/3D 变换；节点不是 2D/3D 时返回 false。</summary>
        /// <param name="node">目标节点。</param>
        /// <param name="position">位置分量。</param>
        /// <param name="rotation">欧拉角分量。</param>
        /// <param name="scale">缩放分量。</param>
        /// <returns>是否应用成功。</returns>
        public static bool ApplyTransform(Node node, float[] position, float[] rotation, float[] scale)
        {
            if (node is Node3D node3D)
            {
                node3D.Position = new Vector3(position[0], position.Length > 1 ? position[1] : 0f, position.Length > 2 ? position[2] : 0f);
                node3D.RotationDegrees = new Vector3(rotation[0], rotation.Length > 1 ? rotation[1] : 0f, rotation.Length > 2 ? rotation[2] : 0f);
                node3D.Scale = new Vector3(scale[0], scale.Length > 1 ? scale[1] : 1f, scale.Length > 2 ? scale[2] : 1f);
                return true;
            }

            if (node is Node2D node2D)
            {
                node2D.Position = new Vector2(position[0], position.Length > 1 ? position[1] : 0f);
                node2D.RotationDegrees = rotation[0];
                node2D.Scale = new Vector2(scale[0], scale.Length > 1 ? scale[1] : 1f);
                return true;
            }

            return false;
        }

        /// <summary>写出修改结果。</summary>
        /// <param name="op">操作名。</param>
        /// <param name="path">目标路径。</param>
        /// <param name="undo">Undo 状态文本。</param>
        /// <param name="extra">额外字段（可为空）。</param>
        /// <returns>JSON 文本。</returns>
        public static string WriteResult(string op, string path, string undo, KeyValuePair<string, string>? extra = null)
        {
            YokiFrameEngineJsonBuilder builder = new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("operation", "scene_mutate")
                .Property("op", op)
                .Property("applied", true)
                .Property("path", path)
                .Property("undo", undo);
            if (extra.HasValue)
            {
                builder.Property(extra.Value.Key, extra.Value.Value);
            }

            return builder.EndObject().ToString();
        }
    }
}
#endif
