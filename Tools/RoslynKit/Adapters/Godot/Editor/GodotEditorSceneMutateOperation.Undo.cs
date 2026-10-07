#if GODOT && TOOLS
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class GodotEditorSceneMutateOperation
    {
        /// <summary>
        /// 校验创建参数并实例化节点。失败时不把节点挂进场景；调用方只在成功后改名和登记 Undo。
        /// </summary>
        /// <param name="sceneRoot">编辑中的场景根，空 parent 时作为父节点。</param>
        /// <param name="root">含 name、nodeType 和可选 parent 的载荷。</param>
        /// <param name="name">非空节点名。</param>
        /// <param name="parentPath">父路径；空表示直接挂到场景根。</param>
        /// <param name="parent">解析到的父节点。</param>
        /// <param name="created">尚未入树的新节点。</param>
        /// <param name="failure">失败时的命令结果。</param>
        /// <returns>参数和父节点都可用时返回 true。</returns>
        private static bool TryPrepareCreate(
            Node sceneRoot,
            JsonElement root,
            out string name,
            out string parentPath,
            out Node parent,
            out Node created,
            out YokiFrameCommandResult failure)
        {
            name = GodotSceneMutateSupport.ReadString(root, "name");
            parentPath = string.Empty;
            parent = null;
            created = null;
            failure = null;
            if (name.Length == 0)
            {
                failure = YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate create requires a non-empty name.");
                return false;
            }

            string nodeType = GodotSceneMutateSupport.ReadString(root, "nodeType");
            created = GodotSceneMutateSupport.CreateNode(nodeType.Length == 0 ? "Node" : nodeType);
            if (created == null)
            {
                failure = YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate nodeType was not found: " + nodeType + ".");
                return false;
            }

            parentPath = GodotSceneMutateSupport.ReadString(root, "parent");
            parent = string.IsNullOrEmpty(parentPath) ? sceneRoot : GodotSceneQuerySupport.FindByPath(sceneRoot, parentPath);
            if (parent != null)
            {
                return true;
            }

            failure = YokiFrameCommandResult.Error(
                RoslynErrorCodes.INVALID_PAYLOAD,
                "scene_mutate parent was not found: " + parentPath + ".");
            return false;
        }

        /// <summary>登记创建的 Do/Undo。管理器缺失或提交抛错时返回 unavailable，由调用方直接挂节点。</summary>
        /// <param name="parent">新节点的父节点。</param>
        /// <param name="created">尚未入树的新节点。</param>
        /// <param name="name">写入动作名的节点名。</param>
        /// <returns>registered 或 unavailable。</returns>
        private static string TryRegisterCreateUndo(Node parent, Node created, string name)
        {
            try
            {
                EditorUndoRedoManager manager = EditorInterface.Singleton.GetEditorUndoRedo();
                if (manager == null)
                {
                    return "unavailable";
                }

                manager.CreateAction("YokiFrame scene_mutate create " + name, UndoRedo.MergeMode.Disable);
                manager.AddDoMethod(parent, Node.MethodName.AddChild, created);
                manager.AddUndoMethod(created, Node.MethodName.QueueFree);
                manager.CommitAction();
                return "registered";
            }
            catch (System.Exception)
            {
                return "unavailable";
            }
        }

        /// <summary>
        /// 读取变换目标与三个数组。任一数组非法或目标不是二维/三维节点时失败，不写属性。
        /// </summary>
        /// <param name="sceneRoot">编辑中的场景根。</param>
        /// <param name="root">含 path 与三个数值数组的载荷。</param>
        /// <param name="target">命中的 Node2D 或 Node3D。</param>
        /// <param name="position">位置分量。</param>
        /// <param name="rotation">旋转分量。</param>
        /// <param name="scale">缩放分量。</param>
        /// <param name="failure">失败时的命令结果。</param>
        /// <returns>可以开始登记或直接应用时返回 true。</returns>
        private static bool TryReadTransform(
            Node sceneRoot,
            JsonElement root,
            out Node target,
            out float[] position,
            out float[] rotation,
            out float[] scale,
            out YokiFrameCommandResult failure)
        {
            position = null;
            rotation = null;
            scale = null;
            failure = ResolveTarget(sceneRoot, root, out target);
            if (failure != null)
            {
                return false;
            }

            if (!GodotSceneMutateSupport.TryReadVector(root, "position", 2, out position)
                || !GodotSceneMutateSupport.TryReadVector(root, "rotation", 1, out rotation)
                || !GodotSceneMutateSupport.TryReadVector(root, "scale", 1, out scale))
            {
                failure = YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_mutate setTransform requires numeric position/rotation/scale arrays.");
                return false;
            }

            if (target is Node3D || target is Node2D)
            {
                return true;
            }

            failure = YokiFrameCommandResult.Error(
                RoslynErrorCodes.UNAVAILABLE,
                "Target node is neither Node2D nor Node3D: " + target.GetClass() + ".");
            return false;
        }

        /// <summary>
        /// 一次动作登记位置、旋转和缩放。管理器缺失或中途失败时返回 unavailable，
        /// 调用方再直接应用，避免只提交其中一部分。
        /// </summary>
        /// <param name="target">Node2D 或 Node3D。</param>
        /// <param name="position">位置分量。</param>
        /// <param name="rotation">旋转分量。</param>
        /// <param name="scale">缩放分量。</param>
        /// <returns>registered 或 unavailable。</returns>
        private static string TryRegisterTransformUndo(Node target, float[] position, float[] rotation, float[] scale)
        {
            try
            {
                EditorUndoRedoManager manager = EditorInterface.Singleton.GetEditorUndoRedo();
                if (manager == null)
                {
                    return "unavailable";
                }

                manager.CreateAction("YokiFrame scene_mutate setTransform " + target.Name, UndoRedo.MergeMode.Disable);
                RecordTransform(manager, target, "position", BuildPositionVariant(target, position));
                RecordTransform(manager, target, "rotation_degrees", BuildRotationVariant(target, rotation));
                RecordTransform(manager, target, "scale", BuildScaleVariant(target, scale));
                manager.CommitAction();
                return "registered";
            }
            catch (System.Exception)
            {
                return "unavailable";
            }
        }
    }
}
#endif
