#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// scene_mutate（godot-editor）：在当前编辑场景里创建/删除/改属性/保存，并登记 Undo。
    /// </summary>
    /// <remarks>
    /// 结构与 Unity 侧一致（op/path/name/nodeType/active/position/rotation/scale），
    /// 差别只在引擎语义：Godot 用 EditorUndoRedoManager 登记动作，删除是 QueueFree，
    /// 保存走 EditorInterface.SaveScene。Undo 不可用时如实回报 <c>undo=unavailable</c>。
    /// </remarks>
    public sealed class GodotEditorSceneMutateOperation : IYokiFrameEngineOperation
    {
        /// <summary>创建编辑器 scene_mutate 操作。</summary>
        public GodotEditorSceneMutateOperation()
        {
            Descriptor = new YokiFrameEngineOperationDescriptor(
                "scene_mutate",
                YokiFrameCommandKind.Dangerous,
                isDiagnostic: false,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTarget.Editor);
        }

        /// <summary>获取操作描述。</summary>
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        /// <summary>执行一次场景修改。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!GodotSceneMutateSupport.TryReadPayload(
                    request.PayloadJson,
                    out string op,
                    out JsonDocument document,
                    out string error))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, error);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (op == GodotSceneMutateSupport.OP_SAVE)
                {
                    return Save();
                }

                Node sceneRoot;
                try
                {
                    EditorInterface editor = EditorInterface.Singleton;
                    sceneRoot = editor == null ? null : editor.GetEditedSceneRoot();
                }
                catch (Exception exception)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.UNAVAILABLE,
                        "EditorInterface is not available in this process: " + exception.Message);
                }

                if (sceneRoot == null)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.UNAVAILABLE,
                        "No scene is currently being edited.");
                }

                switch (op)
                {
                    case GodotSceneMutateSupport.OP_CREATE:
                        return Create(sceneRoot, root);
                    case GodotSceneMutateSupport.OP_DELETE:
                        return Delete(sceneRoot, root);
                    case GodotSceneMutateSupport.OP_SET_ACTIVE:
                        return SetActive(sceneRoot, root);
                    case GodotSceneMutateSupport.OP_SET_TRANSFORM:
                        return SetTransform(sceneRoot, root);
                    default:
                        return YokiFrameCommandResult.Error(
                            YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                            "Unsupported scene_mutate op: " + op + ".");
                }
            }
        }

        private static YokiFrameCommandResult Save()
        {
            try
            {
                EditorInterface editor = EditorInterface.Singleton;
                if (editor == null)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.UNAVAILABLE,
                        "EditorInterface is not available in this process.");
                }

                Error result = editor.SaveScene();
                if (result != Error.Ok)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.FAILED,
                        "Scene save failed with " + result + ".");
                }

                return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                    GodotSceneMutateSupport.OP_SAVE,
                    string.Empty,
                    "n/a"));
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.FAILED,
                    "Scene save failed: " + exception.Message);
            }
        }

        private static YokiFrameCommandResult Create(Node sceneRoot, JsonElement root)
        {
            string name = GodotSceneMutateSupport.ReadString(root, "name");
            if (name.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate create requires a non-empty name.");
            }

            string nodeType = GodotSceneMutateSupport.ReadString(root, "nodeType");
            Node created = GodotSceneMutateSupport.CreateNode(nodeType.Length == 0 ? "Node" : nodeType);
            if (created == null)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate nodeType was not found: " + nodeType + ".");
            }

            string parentPath = GodotSceneMutateSupport.ReadString(root, "parent");
            Node parent = string.IsNullOrEmpty(parentPath) ? sceneRoot : GodotSceneQuerySupport.FindByPath(sceneRoot, parentPath);
            if (parent == null)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate parent was not found: " + parentPath + ".");
            }

            created.Name = name;
            string undo = "unavailable";
            try
            {
                EditorUndoRedoManager manager = EditorInterface.Singleton.GetEditorUndoRedo();
                if (manager != null)
                {
                    manager.CreateAction("YokiFrame scene_mutate create " + name, UndoRedo.MergeMode.Disable);
                    manager.AddDoMethod(parent, Node.MethodName.AddChild, created);
                    manager.AddUndoMethod(created, Node.MethodName.QueueFree);
                    manager.CommitAction();
                    undo = "registered";
                }
            }
            catch (Exception)
            {
                undo = "unavailable";
            }

            if (undo != "registered")
            {
                parent.AddChild(created);
            }

            MarkUnsaved();
            string path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_CREATE,
                path,
                undo,
                new KeyValuePair<string, string>("nodeType", created.GetClass())));
        }

        private static YokiFrameCommandResult Delete(Node sceneRoot, JsonElement root)
        {
            Node target;
            YokiFrameCommandResult failure = ResolveTarget(sceneRoot, root, out target);
            if (failure != null)
            {
                return failure;
            }

            string path = target.GetPath().ToString();
            Node parent = target.GetParent();
            string undo = "unavailable";
            try
            {
                EditorUndoRedoManager manager = EditorInterface.Singleton.GetEditorUndoRedo();
                if (manager != null && parent != null)
                {
                    manager.CreateAction("YokiFrame scene_mutate delete " + target.Name, UndoRedo.MergeMode.Disable);
                    manager.AddDoMethod(target, Node.MethodName.QueueFree);
                    manager.AddUndoMethod(parent, Node.MethodName.AddChild, target);
                    manager.CommitAction();
                    undo = "registered";
                }
            }
            catch (Exception)
            {
                undo = "unavailable";
            }

            if (undo != "registered")
            {
                target.QueueFree();
            }

            MarkUnsaved();
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_DELETE,
                path,
                undo,
                new KeyValuePair<string, string>("deferred", "true")));
        }

        private static YokiFrameCommandResult SetActive(Node sceneRoot, JsonElement root)
        {
            Node target;
            YokiFrameCommandResult failure = ResolveTarget(sceneRoot, root, out target);
            if (failure != null)
            {
                return failure;
            }

            if (!GodotSceneMutateSupport.TryReadBoolean(root, "active", out bool active))
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate setActive requires a boolean active field.");
            }

            bool viaVisibility = target is CanvasItem || target is Node3D;
            string propertyName = viaVisibility ? "visible" : "process_mode";
            Variant previous = target.Get(propertyName);
            Variant next = viaVisibility ? Variant.From(active) : Variant.From(active ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled);
            string undo = ApplyPropertyWithUndo(target, propertyName, previous, next);
            if (undo != "registered")
            {
                GodotSceneMutateSupport.ApplyActive(target, active);
            }

            MarkUnsaved();
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_SET_ACTIVE,
                target.GetPath().ToString(),
                undo,
                new KeyValuePair<string, string>("mechanism", viaVisibility ? "visible" : "processMode")));
        }

        private static YokiFrameCommandResult SetTransform(Node sceneRoot, JsonElement root)
        {
            Node target;
            YokiFrameCommandResult failure = ResolveTarget(sceneRoot, root, out target);
            if (failure != null)
            {
                return failure;
            }

            if (!GodotSceneMutateSupport.TryReadVector(root, "position", 2, out float[] position)
                || !GodotSceneMutateSupport.TryReadVector(root, "rotation", 1, out float[] rotation)
                || !GodotSceneMutateSupport.TryReadVector(root, "scale", 1, out float[] scale))
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate setTransform requires numeric position/rotation/scale arrays.");
            }

            if (target is not Node3D && target is not Node2D)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.UNAVAILABLE,
                    "Target node is neither Node2D nor Node3D: " + target.GetClass() + ".");
            }

            string undo = "registered";
            try
            {
                EditorUndoRedoManager manager = EditorInterface.Singleton.GetEditorUndoRedo();
                if (manager == null)
                {
                    undo = "unavailable";
                }
                else
                {
                    manager.CreateAction("YokiFrame scene_mutate setTransform " + target.Name, UndoRedo.MergeMode.Disable);
                    RecordTransform(manager, target, "position", BuildPositionVariant(target, position));
                    RecordTransform(manager, target, "rotation_degrees", BuildRotationVariant(target, rotation));
                    RecordTransform(manager, target, "scale", BuildScaleVariant(target, scale));
                    manager.CommitAction();
                }
            }
            catch (Exception)
            {
                undo = "unavailable";
            }

            if (undo != "registered")
            {
                GodotSceneMutateSupport.ApplyTransform(target, position, rotation, scale);
            }

            MarkUnsaved();
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_SET_TRANSFORM,
                target.GetPath().ToString(),
                undo));
        }

        private static void RecordTransform(EditorUndoRedoManager manager, Node target, string property, Variant next)
        {
            Variant previous = target.Get(property);
            manager.AddDoProperty(target, property, next);
            manager.AddUndoProperty(target, property, previous);
        }

        private static Variant BuildPositionVariant(Node target, float[] values)
        {
            return target is Node3D
                ? Variant.From(new Vector3(values[0], values.Length > 1 ? values[1] : 0f, values.Length > 2 ? values[2] : 0f))
                : Variant.From(new Vector2(values[0], values.Length > 1 ? values[1] : 0f));
        }

        private static Variant BuildRotationVariant(Node target, float[] values)
        {
            return target is Node3D
                ? Variant.From(new Vector3(values[0], values.Length > 1 ? values[1] : 0f, values.Length > 2 ? values[2] : 0f))
                : Variant.From(values[0]);
        }

        private static Variant BuildScaleVariant(Node target, float[] values)
        {
            return target is Node3D
                ? Variant.From(new Vector3(values[0], values.Length > 1 ? values[1] : 1f, values.Length > 2 ? values[2] : 1f))
                : Variant.From(new Vector2(values[0], values.Length > 1 ? values[1] : 1f));
        }

        private static string ApplyPropertyWithUndo(Node target, string property, Variant previous, Variant next)
        {
            try
            {
                EditorUndoRedoManager manager = EditorInterface.Singleton.GetEditorUndoRedo();
                if (manager == null)
                {
                    return "unavailable";
                }

                manager.CreateAction("YokiFrame scene_mutate setActive " + target.Name, UndoRedo.MergeMode.Disable);
                manager.AddDoProperty(target, property, next);
                manager.AddUndoProperty(target, property, previous);
                manager.CommitAction();
                return "registered";
            }
            catch (Exception)
            {
                return "unavailable";
            }
        }

        private static void MarkUnsaved()
        {
            try
            {
                EditorInterface.Singleton?.MarkSceneAsUnsaved();
            }
            catch (Exception)
            {
                // 标记失败不影响已经完成的修改。
            }
        }

        private static YokiFrameCommandResult ResolveTarget(Node sceneRoot, JsonElement root, out Node target)
        {
            target = null;
            string path = GodotSceneMutateSupport.ReadString(root, "path");
            if (path.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate requires a non-empty path field.");
            }

            target = GodotSceneQuerySupport.FindByPath(sceneRoot, path);
            if (target == null)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate target was not found: " + path + ".");
            }

            return null;
        }
    }
}
#endif
