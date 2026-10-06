#if GODOT && TOOLS
using System.Collections.Generic;
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// scene_mutate（godot-runtime）：在运行中的场景树上创建/删除/改属性。
    /// </summary>
    /// <remarks>
    /// Runtime 没有编辑器语义的 Undo，也无法保存场景：响应里如实回报
    /// <c>undo=unavailable-in-runtime</c>，save 返回 <c>EngineOperationUnavailable</c>，而不是假装成功。
    /// 删除使用 QueueFree（Godot 的延迟释放），响应标注 <c>deferred=true</c>。
    /// </remarks>
    public sealed class GodotRuntimeSceneMutateOperation : IYokiFrameEngineOperation
    {
        private readonly Node mOwner;

        /// <summary>创建 runtime scene_mutate 操作。</summary>
        /// <param name="owner">用于获取 SceneTree 的节点。</param>
        public GodotRuntimeSceneMutateOperation(Node owner)
        {
            mOwner = owner;
            Descriptor = new YokiFrameEngineOperationDescriptor(
                "scene_mutate",
                YokiFrameCommandKind.Dangerous,
                isDiagnostic: false,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTarget.Runtime);
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
                Node sceneRoot = ResolveSceneRoot();
                if (sceneRoot == null)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.UNAVAILABLE,
                        "SceneTree root is not available for scene_mutate.");
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
                    case GodotSceneMutateSupport.OP_SAVE:
                        return YokiFrameCommandResult.Error(
                            YokiFrameEngineErrorCodes.UNAVAILABLE,
                            "godot-runtime cannot save scenes; run scene_mutate save on godot-editor.");
                    default:
                        return YokiFrameCommandResult.Error(
                            YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                            "Unsupported scene_mutate op: " + op + ".");
                }
            }
        }

        private Node ResolveSceneRoot()
        {
            SceneTree tree = mOwner == null ? null : mOwner.GetTree();
            return tree == null ? null : tree.Root;
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
            parent.AddChild(created);
            string path = parent == sceneRoot && string.IsNullOrEmpty(parentPath)
                ? name
                : (string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name);
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_CREATE,
                path,
                "unavailable-in-runtime",
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
            target.QueueFree();
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_DELETE,
                path,
                "unavailable-in-runtime",
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

            bool viaVisibility = GodotSceneMutateSupport.ApplyActive(target, active);
            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_SET_ACTIVE,
                target.GetPath().ToString(),
                "unavailable-in-runtime",
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

            if (!GodotSceneMutateSupport.ApplyTransform(target, position, rotation, scale))
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.UNAVAILABLE,
                    "Target node is neither Node2D nor Node3D: " + target.GetClass() + ".");
            }

            return YokiFrameCommandResult.Success(GodotSceneMutateSupport.WriteResult(
                GodotSceneMutateSupport.OP_SET_TRANSFORM,
                target.GetPath().ToString(),
                "unavailable-in-runtime"));
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
