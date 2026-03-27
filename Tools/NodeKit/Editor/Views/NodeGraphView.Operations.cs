using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public partial class NodeGraphView
    {
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // 处理移除的元素
            if (change.elementsToRemove != default)
            {
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    var element = change.elementsToRemove[i];
                    if (element is EdgeView edge)
                        OnEdgeRemoved(edge);
                    else if (element is NodeView nodeView)
                        OnNodeRemoved(nodeView);
                }
            }

            // 处理创建的边
            if (change.edgesToCreate != default)
            {
                for (int i = 0; i < change.edgesToCreate.Count; i++)
                {
                    var edge = change.edgesToCreate[i];
                    OnEdgeCreated(edge);
                }
            }

            // 自动保存
            if (NodePreferences.AutoSave)
                SaveGraph();

            return change;
        }

        private void OnEdgeCreated(Edge edge)
        {
            if (edge.output is not PortView outputPort) return;
            if (edge.input is not PortView inputPort) return;

            NodeEditorUtility.RecordUndo(mGraph, "Create Connection");
            outputPort.NodePort.Connect(inputPort.NodePort);

            outputPort.NodeView.Target.OnCreateConnection(outputPort.NodePort, inputPort.NodePort);
            inputPort.NodeView.Target.OnCreateConnection(outputPort.NodePort, inputPort.NodePort);
        }

        private void OnEdgeRemoved(EdgeView edge)
        {
            if (edge.OutputPort == default || edge.InputPort == default) return;

            NodeEditorUtility.RecordUndo(mGraph, "Remove Connection");
            edge.OutputPort.Disconnect(edge.InputPort);

            edge.OutputPort.Node.OnRemoveConnection(edge.OutputPort);
            edge.InputPort.Node.OnRemoveConnection(edge.InputPort);
        }

        private void OnNodeRemoved(NodeView nodeView)
        {
            if (nodeView == default || nodeView.Target == default) return;
            if (mGraph == default) return;

            NodeEditorUtility.RecordUndo(mGraph, "Delete Node");
            mNodeViews.Remove(nodeView.Target);
            mGraph.RemoveNode(nodeView.Target);
        }

        /// <summary>
        /// 创建节点
        /// </summary>
        public Node CreateNode(System.Type type, Vector2 position)
        {
            if (mGraph == default || mGraphEditor == default) return null;

            // 检查 DisallowMultipleNodes
            if (HasDisallowMultipleNodes(type))
            {
                Debug.LogWarning($"[NodeKit] Only one {type.Name} node is allowed per graph.");
                return null;
            }

            NodeEditorUtility.RecordUndo(mGraph, "Create Node");
            var node = mGraphEditor.CreateNode(type, position);
            if (node == default) return null;

            CreateNodeView(node);
            return node;
        }

        private bool HasDisallowMultipleNodes(System.Type type)
        {
            var attr = type.GetCustomAttributes(typeof(DisallowMultipleNodesAttribute), true);
            if (attr.Length == 0) return false;

            var nodes = mGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != default && nodes[i].GetType() == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 删除节点
        /// </summary>
        public void DeleteNode(NodeView nodeView)
        {
            if (nodeView == default || mGraph == default) return;
            DeleteElements(new GraphElement[] { nodeView });
        }

        /// <summary>
        /// 复制节点
        /// </summary>
        public NodeView DuplicateNode(NodeView nodeView)
        {
            if (nodeView == default || mGraph == default) return null;

            NodeEditorUtility.RecordUndo(mGraph, "Duplicate Node");
            var copy = mGraph.CopyNode(nodeView.Target);
            if (copy == default) return null;

            copy.Position = nodeView.Target.Position + new Vector2(30, 30);
            return CreateNodeView(copy);
        }

        /// <summary>
        /// 框选所有节点（调整视图以显示所有节点）
        /// </summary>
        public new void FrameAll() => base.FrameAll();

        /// <summary>
        /// 框选选中节点
        /// </summary>
        public void FrameSelected() => FrameSelection();

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (mGraph == default) return;

            var mousePos = evt.localMousePosition;
            var nodeTypes = NodeReflection.GetNodeTypes();

            for (int i = 0; i < nodeTypes.Count; i++)
            {
                var info = nodeTypes[i];
                evt.menu.AppendAction(
                    $"Create/{info.MenuPath}",
                    _ => CreateNode(info.Type, mousePos));
            }

            evt.menu.AppendSeparator();
            mGraphEditor.BuildContextMenu(evt);
        }
    }
}
