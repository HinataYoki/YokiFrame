#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// scene_mutate：创建/删除/激活/改变换/保存，全部登记 Undo 并标记场景脏。
    /// </summary>
    /// <remarks>
    /// 单次调用只改一个对象，不做批量遍历（列表式修改交给入口执行器，由用户代码分帧推进）；
    /// 播放态下 Unity 的 Undo 语义不同，响应里明确回报 <c>undo</c> 状态而不是假装可用。
    /// </remarks>
    internal sealed class UnitySceneMutateOperation : IRoslynOperation
    {
        internal const string ACTION = "scene_mutate";

        private const string OP_CREATE = "create";
        private const string OP_DELETE = "delete";
        private const string OP_SET_ACTIVE = "setActive";
        private const string OP_SET_TRANSFORM = "setTransform";
        private const string OP_SAVE = "save";

        /// <summary>创建 scene_mutate 操作。</summary>
        internal UnitySceneMutateOperation()
        {
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.Dangerous,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行一次场景修改。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!TryReadPayload(request.PayloadJson, out string op, out JsonDocument document, out string error))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                switch (op)
                {
                    case OP_CREATE:
                        return Create(root);
                    case OP_DELETE:
                        return Delete(root);
                    case OP_SET_ACTIVE:
                        return SetActive(root);
                    case OP_SET_TRANSFORM:
                        return SetTransform(root);
                    case OP_SAVE:
                        return Save();
                    default:
                        return YokiFrameCommandResult.Error(
                            RoslynErrorCodes.INVALID_PAYLOAD,
                            "Unsupported scene_mutate op: " + op + ".");
                }
            }
        }

        private static YokiFrameCommandResult Create(JsonElement root)
        {
            if (!TryReadPath(root, out string parentPath, out string pathError))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, pathError);
            }

            string name = ReadString(root, "name");
            if (name.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate create requires a non-empty name.");
            }

            GameObject parent = parentPath.Length == 0 ? null : FindByPath(parentPath);
            if (parentPath.Length > 0 && parent == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate parent was not found: " + parentPath + ".");
            }

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "YokiFrame scene_mutate create");
            if (parent != null)
            {
                created.transform.SetParent(parent.transform, worldPositionStays: false);
            }

            string componentError = AddComponents(created, root);
            if (componentError.Length > 0)
            {
                Undo.DestroyObjectImmediate(created);
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, componentError);
            }

            MarkDirty(created);
            string path = parentPath.Length == 0 ? name : parentPath + "/" + name;
            return YokiFrameCommandResult.Success(WriteResult(OP_CREATE, path, created.scene));
        }

        private static YokiFrameCommandResult Delete(JsonElement root)
        {
            if (!TryReadPath(root, out string path, out string pathError))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, pathError);
            }

            GameObject target = FindByPath(path);
            if (target == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate target was not found: " + path + ".");
            }

            Scene scene = target.scene;
            Undo.DestroyObjectImmediate(target);
            MarkDirty(scene);
            return YokiFrameCommandResult.Success(WriteResult(OP_DELETE, path, scene));
        }

        private static YokiFrameCommandResult SetActive(JsonElement root)
        {
            if (!TryReadPath(root, out string path, out string pathError))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, pathError);
            }

            if (!root.TryGetProperty("active", out JsonElement active)
                || (active.ValueKind != JsonValueKind.True && active.ValueKind != JsonValueKind.False))
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate setActive requires a boolean active field.");
            }

            GameObject target = FindByPath(path);
            if (target == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate target was not found: " + path + ".");
            }

            Undo.RecordObject(target, "YokiFrame scene_mutate setActive");
            target.SetActive(active.ValueKind == JsonValueKind.True);
            MarkDirty(target);
            return YokiFrameCommandResult.Success(WriteResult(OP_SET_ACTIVE, path, target.scene));
        }

        private static YokiFrameCommandResult SetTransform(JsonElement root)
        {
            if (!TryReadPath(root, out string path, out string pathError))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, pathError);
            }

            GameObject target = FindByPath(path);
            if (target == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate target was not found: " + path + ".");
            }

            if (!TryReadVector(root, "position", out Vector3 position)
                || !TryReadVector(root, "rotation", out Vector3 rotation)
                || !TryReadVector(root, "scale", out Vector3 scale))
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate setTransform requires numeric position/rotation/scale arrays of length 3.");
            }

            Undo.RecordObject(target.transform, "YokiFrame scene_mutate setTransform");
            target.transform.localPosition = position;
            target.transform.localEulerAngles = rotation;
            target.transform.localScale = scale;
            MarkDirty(target);
            return YokiFrameCommandResult.Success(WriteResult(OP_SET_TRANSFORM, path, target.scene));
        }

        private static YokiFrameCommandResult Save()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "No active scene is available to save.");
            }

            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.FAILED,
                    "Scene save failed or was cancelled: " + scene.path + ".");
            }

            return YokiFrameCommandResult.Success(WriteResult(OP_SAVE, scene.path, scene));
        }

        private static string AddComponents(GameObject target, JsonElement root)
        {
            if (!root.TryGetProperty("components", out JsonElement components)
                || components.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            for (var index = 0; index < components.GetArrayLength(); index++)
            {
                string typeName = components[index].GetString();
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return "scene_mutate components entries must be non-empty type names.";
                }

                Type type = ResolveComponentType(typeName.Trim());
                if (type == null)
                {
                    return "Component type was not found: " + typeName + ".";
                }

                Undo.AddComponent(target, type);
            }

            return string.Empty;
        }

        private static Type ResolveComponentType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            string qualified = "UnityEngine." + typeName;
            type = Type.GetType(qualified);
            if (type != null)
            {
                return type;
            }

            foreach (System.Reflection.Assembly assembly in LoadedAssemblies.Get())
            {
                type = assembly.GetType(typeName) ?? assembly.GetType(qualified);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void MarkDirty(GameObject target)
        {
            if (target != null)
            {
                MarkDirty(target.scene);
            }
        }

        private static void MarkDirty(Scene scene)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static string WriteResult(string op, string path, Scene scene)
        {
            return new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("op", op)
                .Property("applied", true)
                .Property("path", path)
                .Property("scene", scene.IsValid() ? scene.name : string.Empty)
                .Property("isDirty", scene.IsValid() && scene.isDirty)
                .Property("undo", EditorApplication.isPlaying ? "unavailable-in-playmode" : "registered")
                .EndObject()
                .ToString();
        }

        private static bool TryReadPayload(
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

        private static bool TryReadPath(JsonElement root, out string path, out string error)
        {
            path = ReadString(root, "path");
            error = string.Empty;
            if (path.Length == 0)
            {
                error = "scene_mutate requires a non-empty path field.";
                return false;
            }

            return true;
        }

        private static bool TryReadVector(JsonElement root, string name, out Vector3 value)
        {
            value = Vector3.one;
            if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (element.GetArrayLength() != 3)
            {
                return false;
            }

            float[] parts = new float[3];
            for (var index = 0; index < 3; index++)
            {
                // 变换分量是浮点数：整数读法会把 0.5 这类合法值拒掉，因此按不变文化解析原始文本。
                if (!double.TryParse(
                        element[index].ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double parsed))
                {
                    return false;
                }

                parts[index] = (float)parsed;
            }

            value = new Vector3(parts[0], parts[1], parts[2]);
            return true;
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty).Trim()
                : string.Empty;
        }

        private static GameObject FindByPath(string path)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            string[] segments = path.Split('/');
            GameObject[] level = roots;
            GameObject current = null;
            var found = false;
            for (var index = 0; index < segments.Length; index++)
            {
                string segment = segments[index].Trim();
                if (segment.Length == 0)
                {
                    continue;
                }

                GameObject match = null;
                for (var candidate = 0; candidate < level.Length; candidate++)
                {
                    if (string.Equals(level[candidate].name, segment, StringComparison.Ordinal))
                    {
                        match = level[candidate];
                        break;
                    }
                }

                if (match == null)
                {
                    return null;
                }

                current = match;
                found = true;
                var children = new List<GameObject>(match.transform.childCount);
                for (var childIndex = 0; childIndex < match.transform.childCount; childIndex++)
                {
                    children.Add(match.transform.GetChild(childIndex).gameObject);
                }

                level = children.ToArray();
            }

            return found ? current : null;
        }
    }
}
#endif
