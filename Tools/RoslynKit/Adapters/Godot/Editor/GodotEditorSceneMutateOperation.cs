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
    public sealed partial class GodotEditorSceneMutateOperation : IRoslynOperation
    {
        /// <summary>创建编辑器 scene_mutate 操作。</summary>
        public GodotEditorSceneMutateOperation()
        {
            Descriptor = new RoslynOperationDescriptor(
                "scene_mutate",
                YokiFrameCommandKind.Dangerous,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Editor);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行一次场景修改。保存不依赖当前编辑根，其余操作先取根再分发。</summary>
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
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            using (document)
            {
                if (op == GodotSceneMutateSupport.OP_SAVE)
                {
                    return Save();
                }

                if (!TryGetEditedRoot(out Node sceneRoot, out YokiFrameCommandResult unavailable))
                {
                    return unavailable;
                }

                return Dispatch(op, sceneRoot, document.RootElement);
            }
        }

        /// <summary>读取当前编辑场景根。编辑器接口不可用或没有打开场景时返回错误，不抛出。</summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="failure">不可用时的命令结果。</param>
        /// <returns>拿到根节点时返回 true。</returns>
        private static bool TryGetEditedRoot(out Node sceneRoot, out YokiFrameCommandResult failure)
        {
            sceneRoot = null;
            failure = null;
            try
            {
                EditorInterface editor = EditorInterface.Singleton;
                sceneRoot = editor == null ? null : editor.GetEditedSceneRoot();
            }
            catch (Exception exception)
            {
                failure = YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "EditorInterface is not available in this process: " + exception.Message);
                return false;
            }

            if (sceneRoot != null)
            {
                return true;
            }

            failure = YokiFrameCommandResult.Error(
                RoslynErrorCodes.UNAVAILABLE,
                "No scene is currently being edited.");
            return false;
        }

        /// <summary>按 op 分发到具体修改。未知 op 原样返回不支持，不猜测语义。</summary>
        /// <param name="op">已校验的操作名。</param>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">payload 根对象。</param>
        /// <returns>命令结果。</returns>
        private static YokiFrameCommandResult Dispatch(string op, Node sceneRoot, JsonElement root)
        {
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
                        RoslynErrorCodes.INVALID_PAYLOAD,
                        "Unsupported scene_mutate op: " + op + ".");
            }
        }

        /// <summary>保存当前编辑场景。编辑器接口缺失或保存返回非 Ok 时失败，不假装已保存。</summary>
        /// <returns>保存结果。</returns>
        private static YokiFrameCommandResult Save()
        {
            try
            {
                EditorInterface editor = EditorInterface.Singleton;
                if (editor == null)
                {
                    return YokiFrameCommandResult.Error(
                        RoslynErrorCodes.UNAVAILABLE,
                        "EditorInterface is not available in this process.");
                }

                Error result = editor.SaveScene();
                if (result != Error.Ok)
                {
                    return YokiFrameCommandResult.Error(
                        RoslynErrorCodes.FAILED,
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
                    RoslynErrorCodes.FAILED,
                    "Scene save failed: " + exception.Message);
            }
        }

        /// <summary>创建节点并尽量登记 Undo。Undo 不可用时直接挂到父节点，结果里回报 unavailable。</summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">含 name、nodeType 和可选 parent 的载荷。</param>
        /// <returns>创建结果，或名字、类型、父路径错误。</returns>
        private static YokiFrameCommandResult Create(Node sceneRoot, JsonElement root)
        {
            if (!TryPrepareCreate(sceneRoot, root, out string name, out string parentPath, out Node parent, out Node created, out YokiFrameCommandResult failure))
            {
                return failure;
            }

            created.Name = name;
            string undo = TryRegisterCreateUndo(parent, created, name);
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

        /// <summary>按路径删除节点。有父节点且 Undo 可用时登记，否则直接 QueueFree。</summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">含 path 的载荷。</param>
        /// <returns>删除结果，或目标不存在的错误。</returns>
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

        /// <summary>切换节点活动状态。CanvasItem 与 Node3D 改 visible，其余改 process_mode。</summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">含 path 和 active 的载荷。</param>
        /// <returns>修改结果，或目标、字段错误。</returns>
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
                    RoslynErrorCodes.INVALID_PAYLOAD,
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

        /// <summary>写入变换。非 Node2D/Node3D 直接失败；Undo 失败时立即应用，不留下半登记动作。</summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">含 path 与三个数值数组的载荷。</param>
        /// <returns>修改结果，或目标、字段、节点类型错误。</returns>
        private static YokiFrameCommandResult SetTransform(Node sceneRoot, JsonElement root)
        {
            if (!TryReadTransform(sceneRoot, root, out Node target, out float[] position, out float[] rotation, out float[] scale, out YokiFrameCommandResult failure))
            {
                return failure;
            }

            string undo = TryRegisterTransformUndo(target, position, rotation, scale);
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

        /// <summary>把当前属性值登记为 Undo，并把下一值登记为 Do。不提交动作。</summary>
        /// <param name="manager">已创建动作的 Undo 管理器。</param>
        /// <param name="target">目标节点。</param>
        /// <param name="property">Godot 属性名。</param>
        /// <param name="next">下一值。</param>
        private static void RecordTransform(EditorUndoRedoManager manager, Node target, string property, Variant next)
        {
            Variant previous = target.Get(property);
            manager.AddDoProperty(target, property, next);
            manager.AddUndoProperty(target, property, previous);
        }

        /// <summary>按节点维度构造 position。缺省的后续分量用 0。</summary>
        /// <param name="target">Node2D 或 Node3D。</param>
        /// <param name="values">至少含一个分量的数组。</param>
        /// <returns>可写入 position 的 Variant。</returns>
        private static Variant BuildPositionVariant(Node target, float[] values)
        {
            return target is Node3D
                ? Variant.From(new Vector3(values[0], values.Length > 1 ? values[1] : 0f, values.Length > 2 ? values[2] : 0f))
                : Variant.From(new Vector2(values[0], values.Length > 1 ? values[1] : 0f));
        }

        /// <summary>按节点维度构造旋转。Node2D 只取第一分量。</summary>
        /// <param name="target">Node2D 或 Node3D。</param>
        /// <param name="values">至少含一个分量的数组。</param>
        /// <returns>可写入 rotation_degrees 的 Variant。</returns>
        private static Variant BuildRotationVariant(Node target, float[] values)
        {
            return target is Node3D
                ? Variant.From(new Vector3(values[0], values.Length > 1 ? values[1] : 0f, values.Length > 2 ? values[2] : 0f))
                : Variant.From(values[0]);
        }

        /// <summary>按节点维度构造 scale。缺省的后续分量用 1，避免把节点缩没。</summary>
        /// <param name="target">Node2D 或 Node3D。</param>
        /// <param name="values">至少含一个分量的数组。</param>
        /// <returns>可写入 scale 的 Variant。</returns>
        private static Variant BuildScaleVariant(Node target, float[] values)
        {
            return target is Node3D
                ? Variant.From(new Vector3(values[0], values.Length > 1 ? values[1] : 1f, values.Length > 2 ? values[2] : 1f))
                : Variant.From(new Vector2(values[0], values.Length > 1 ? values[1] : 1f));
        }

        /// <summary>登记单个属性的 Undo。管理器缺失或提交失败时返回 unavailable，不抛出。</summary>
        /// <param name="target">目标节点。</param>
        /// <param name="property">Godot 属性名。</param>
        /// <param name="previous">修改前的值。</param>
        /// <param name="next">修改后的值。</param>
        /// <returns>registered 或 unavailable。</returns>
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

        /// <summary>把当前编辑场景标为未保存。标记失败不影响已经完成的修改。</summary>
        private static void MarkUnsaved()
        {
            try
            {
                EditorInterface editor = EditorInterface.Singleton;
                if (GodotObject.IsInstanceValid(editor)) editor.MarkSceneAsUnsaved();
            }
            catch (Exception)
            {
                // 标记失败不影响已经完成的修改。
            }
        }

        /// <summary>按 path 解析目标。空路径和找不到都返回错误，不改场景。</summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">含 path 的载荷。</param>
        /// <param name="target">命中的节点；失败时为 null。</param>
        /// <returns>失败时的命令结果；成功时返回 null。</returns>
        private static YokiFrameCommandResult ResolveTarget(Node sceneRoot, JsonElement root, out Node target)
        {
            target = null;
            string path = GodotSceneMutateSupport.ReadString(root, "path");
            if (path.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate requires a non-empty path field.");
            }

            target = GodotSceneQuerySupport.FindByPath(sceneRoot, path);
            if (target == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate target was not found: " + path + ".");
            }

            return null;
        }
    }
}
#endif
