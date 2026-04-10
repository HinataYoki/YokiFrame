using System;
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
            if (mSuppressGraphViewChanges)
                return change;

            if (change.elementsToRemove != default)
            {
                for (int i = change.elementsToRemove.Count - 1; i >= 0; i--)
                {
                    if (change.elementsToRemove[i] is NodeView nodeView && !mGraphEditor.CanRemove(nodeView.Target))
                        change.elementsToRemove.RemoveAt(i);
                }
            }

            if (change.elementsToRemove != default)
            {
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    var element = change.elementsToRemove[i];
                    if (element is Edge edge)
                        OnEdgeRemoved(edge);
                    else if (element is NodeView nodeView)
                        OnNodeRemoved(nodeView);
                }
            }

            if (change.edgesToCreate != default)
            {
                for (int i = 0; i < change.edgesToCreate.Count; i++)
                    OnEdgeCreated(change.edgesToCreate[i]);
            }

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
            outputPort.NodeView.RefreshContents();
            inputPort.NodeView.RefreshContents();

            if (NodePreferences.AutoSave)
                SaveGraph();
        }

        private void OnEdgeRemoved(Edge edge)
        {
            if (edge.output is not PortView outputPortView || edge.input is not PortView inputPortView) return;

            NodeEditorUtility.RecordUndo(mGraph, "Remove Connection");
            outputPortView.NodePort.Disconnect(inputPortView.NodePort);

            outputPortView.NodePort.Node.OnRemoveConnection(outputPortView.NodePort);
            inputPortView.NodePort.Node.OnRemoveConnection(inputPortView.NodePort);
            outputPortView.NodeView.RefreshContents();
            inputPortView.NodeView.RefreshContents();

            if (NodePreferences.AutoSave)
                SaveGraph();
        }

        private void OnNodeRemoved(NodeView nodeView)
        {
            if (nodeView == default || nodeView.Target == default) return;
            if (mGraph == default) return;
            if (!mGraphEditor.CanRemove(nodeView.Target)) return;

            NodeEditorUtility.RecordUndo(mGraph, "Delete Node");
            mNodeViews.Remove(nodeView.Target);
            mGraphEditor.RemoveNode(nodeView.Target);

            if (NodePreferences.AutoSave)
                SaveGraph();
        }

        public Node CreateNode(Type type, Vector2 position)
        {
            if (mGraph == default || mGraphEditor == default) return null;

            if (HasDisallowMultipleNodes(type))
            {
                Debug.LogWarning($"[NodeKit] Only {GetMaxNodeCount(type)} {type.Name} node(s) are allowed per graph.");
                return null;
            }

            NodeEditorUtility.RecordUndo(mGraph, "Create Node");
            var node = mGraphEditor.CreateNode(type, position);
            if (node == default) return null;

            CreateNodeView(node);
            SaveGraph();
            return node;
        }

        private bool HasDisallowMultipleNodes(Type type)
        {
            int maxCount = GetMaxNodeCount(type);
            if (maxCount < 0) return false;
            return !CanCreateNodeType(type, maxCount);
        }

        public bool CanCreateNodeType(Type type, int maxCount = -1)
        {
            if (mGraph == default || type == default)
                return false;

            if (maxCount < 0)
                maxCount = GetMaxNodeCount(type);
            if (maxCount < 0)
                return true;

            int count = 0;
            var nodes = mGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != default && nodes[i].GetType() == type)
                    count++;
            }

            return count < maxCount;
        }

        private static int GetMaxNodeCount(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(DisallowMultipleNodesAttribute), true);
            if (attr.Length == 0) return -1;
            return attr[0] is DisallowMultipleNodesAttribute disallow ? disallow.Max : 1;
        }

        public void DeleteNode(NodeView nodeView)
        {
            if (nodeView == default || mGraph == default) return;
            DeleteElements(new GraphElement[] { nodeView });
        }

        public NodeView DuplicateNode(NodeView nodeView)
        {
            if (nodeView == default || mGraph == default) return null;
            if (HasDisallowMultipleNodes(nodeView.Target.GetType())) return null;

            NodeEditorUtility.RecordUndo(mGraph, "Duplicate Node");
            var copy = mGraph.CopyNode(nodeView.Target);
            if (copy == default) return null;

            copy.Position = nodeView.Target.Position + new Vector2(30, 30);
            var copyView = CreateNodeView(copy);
            RefreshNodeView(copy);
            RefreshConnections(nodeView.Target, copy);
            SaveGraph();
            return copyView;
        }

        public new void FrameAll() => base.FrameAll();

        public void FrameSelected() => FrameSelection();

        public void Home(Node node = null)
        {
            if (node != default)
            {
                var nodeView = GetNodeView(node);
                if (nodeView != default)
                {
                    ClearSelection();
                    AddToSelection(nodeView);
                    FrameSelected();
                    return;
                }
            }

            if (selection.Count > 0) FrameSelected();
            else FrameAll();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (mGraph == default) return;

            var mousePos = evt.localMousePosition;
            var nodeTypes = new List<NodeReflection.NodeTypeInfo>(NodeReflection.GetNodeTypes());
            nodeTypes.Sort((a, b) =>
            {
                string menuA = mGraphEditor.GetNodeMenuName(a.Type);
                string menuB = mGraphEditor.GetNodeMenuName(b.Type);
                int orderCompare = mGraphEditor.GetNodeMenuOrder(a.Type).CompareTo(mGraphEditor.GetNodeMenuOrder(b.Type));
                return orderCompare != 0 ? orderCompare : string.Compare(menuA, menuB, StringComparison.Ordinal);
            });

            for (int i = 0; i < nodeTypes.Count; i++)
            {
                var info = nodeTypes[i];
                string menuName = mGraphEditor.GetNodeMenuName(info.Type);
                if (string.IsNullOrWhiteSpace(menuName))
                    continue;

                bool canCreate = CanCreateNodeType(info.Type, info.MaxCount);
                evt.menu.AppendAction(
                    $"Create/{menuName}",
                    _ => CreateNode(info.Type, mousePos),
                    _ => canCreate ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }

            evt.menu.AppendSeparator();
            var customItems = NodeEditorUtility.GetContextMenuMethods(mGraph);
            for (int i = 0; i < customItems.Length; i++)
            {
                var method = customItems[i].Value;
                var menu = customItems[i].Key;
                evt.menu.AppendAction(menu.menuItem, _ => method.Invoke(mGraph, null));
            }

            if (customItems.Length > 0)
                evt.menu.AppendSeparator();

            evt.menu.AppendAction(
                "Paste",
                _ => PasteNodes(mousePos),
                _ => HasCopyBuffer() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction(
                "Clear Start Node",
                _ => ClearStartNode(),
                _ => mGraph.HasExplicitStartNode ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction(
                "Home Start Node",
                _ => Home(mGraph.StartNode),
                _ => mGraph.StartNode != default ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Preferences", _ => SettingsService.OpenUserPreferences("Preferences/NodeKit"));

            mGraphEditor.BuildContextMenu(evt);
        }

        private void ClearStartNode()
        {
            if (mGraph == default)
                return;

            var previousStartNode = mGraph.StartNode;
            if (previousStartNode == default)
                return;

            NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mGraph, previousStartNode }, "Clear Start Node");
            mGraph.ClearStartNode();
            RefreshStartNodeState(previousStartNode);
            SaveGraph();
        }
    }
}
