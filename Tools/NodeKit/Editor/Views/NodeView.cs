using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using GVNode = UnityEditor.Experimental.GraphView.Node;

namespace YokiFrame.NodeKit.Editor
{
    public partial class NodeView : GVNode
    {
        private Node mTarget;
        private NodeEditorBase mNodeEditor;
        private NodeGraphView mGraphView;
        private Dictionary<string, PortView> mPortViews = new();
        private bool mPositionChanged;

        public Node Target => mTarget;
        public NodeEditorBase NodeEditor => mNodeEditor;
        public NodeGraphView GraphView => mGraphView;

        public NodeView()
        {
            AddToClassList("yoki-node");
            style.borderTopLeftRadius = 12;
            style.borderTopRightRadius = 12;
            style.borderBottomLeftRadius = 12;
            style.borderBottomRightRadius = 12;
            style.overflow = Overflow.Hidden;
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut);
        }

        public void Initialize(Node target, NodeGraphView graphView)
        {
            mTarget = target;
            mGraphView = graphView;

            var editorType = NodeReflection.GetNodeEditorType(target.GetType());
            mNodeEditor = Activator.CreateInstance(editorType) as NodeEditorBase;
            mNodeEditor.Initialize(target, graphView);
            mNodeEditor.SetNodeView(this);

            SetPosition(new Rect(target.Position, Vector2.zero));

            style.width = mNodeEditor.GetWidth();
            style.backgroundColor = new StyleColor(mNodeEditor.GetTint());

            BuildHeader();
            BuildPorts();
            BuildBody();

            RefreshExpandedState();
            RefreshPorts();
        }

        private void BuildHeader()
        {
            var header = titleContainer;
            header.Clear();
            header.AddToClassList("yoki-node__header");
            mNodeEditor.OnHeaderGUI(header);
            if (mGraphView?.Graph?.HasExplicitStartNode == true && mGraphView.Graph.StartNode == mTarget)
            {
                var startBadge = new Label("Start");
                startBadge.AddToClassList("yoki-node__start-badge");
                header.Add(startBadge);
            }
            tooltip = mNodeEditor.GetHeaderTooltip();
        }

        private void BuildBody()
        {
            var body = extensionContainer;
            body.Clear();
            body.AddToClassList("yoki-node__body");
            mNodeEditor.OnBodyGUI(body);
        }

        public void RefreshContents()
        {
            BuildHeader();
            BuildBody();
            RefreshAllPorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        public PortView GetPortView(NodePort port)
        {
            if (port == default) return null;
            return mPortViews.TryGetValue(port.FieldName, out var view) ? view : null;
        }

        public PortView GetPortView(string fieldName)
        {
            return mPortViews.TryGetValue(fieldName, out var view) ? view : null;
        }

        public override void SetPosition(Rect newPos)
        {
            var position = new Vector2(newPos.x, newPos.y);
            if (mTarget != default && mGraphView != default && !mGraphView.SuppressGraphViewChanges && NodePreferences.GridSnap)
            {
                float snap = NodePreferences.GridSnapSize;
                position.x = Mathf.Round(position.x / snap) * snap;
                position.y = Mathf.Round(position.y / snap) * snap;
                newPos.position = position;
            }

            base.SetPosition(newPos);
            if (mTarget == default)
                return;

            if (Vector2.Distance(mTarget.Position, position) <= 0.01f)
                return;

            NodeEditorUtility.RecordUndo(mTarget, "Move Node");
            mTarget.Position = position;
            NodeEditorUtility.SetDirty(mTarget);
            if (mGraphView?.Graph != default)
            {
                NodeEditorUtility.SetDirty(mGraphView.Graph);
                mPositionChanged = true;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            CommitMoveIfNeeded();
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent evt)
        {
            CommitMoveIfNeeded();
        }

        private void CommitMoveIfNeeded()
        {
            if (!mPositionChanged || mGraphView?.Graph == default)
                return;

            mPositionChanged = false;
            if (NodePreferences.AutoSave)
                mGraphView.SaveGraph();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", _ => RenamePopup.Show(mTarget, __ =>
            {
                mNodeEditor.OnRename();
                BuildHeader();
                mGraphView.SaveGraph();
            }));
            AppendCustomContextMenu(evt, mTarget);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Copy", _ => mGraphView.CopyNode(this));
            evt.menu.AppendAction("Duplicate", _ => mGraphView.DuplicateNode(this), _ => CanDuplicate() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Move To Front", _ => mGraphView.MoveNodeToFront(this), _ => CanMoveToFront() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Set As Start Node", _ => SetAsStartNode(), _ => CanSetAsStartNode() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Clear Start Node", _ => ClearStartNode(), _ => CanClearStartNode() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Home", _ => mGraphView.Home(mTarget));
            evt.menu.AppendAction("Delete", _ => mGraphView.DeleteNode(this), _ => CanDelete() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            mNodeEditor.BuildContextMenu(evt);
        }

        private bool CanDuplicate()
        {
            if (mTarget == default || mGraphView?.Graph == default)
                return false;
            return mGraphView.CanCreateNodeType(mTarget.GetType());
        }

        private bool CanDelete()
        {
            return mGraphView != default && mTarget != default && mGraphView.GraphEditor.CanRemove(mTarget);
        }

        private bool CanMoveToFront()
        {
            if (mGraphView?.Graph == default || mTarget == default)
                return false;

            var nodes = mGraphView.Graph.Nodes;
            return nodes.Count > 1 && nodes[^1] != mTarget;
        }

        private bool CanSetAsStartNode()
        {
            return mGraphView?.Graph != default && mTarget != default && mGraphView.Graph.StartNode != mTarget;
        }

        private bool CanClearStartNode()
        {
            return mGraphView?.Graph != default
                && mTarget != default
                && mGraphView.Graph.HasExplicitStartNode
                && mGraphView.Graph.StartNode == mTarget;
        }

        private void SetAsStartNode()
        {
            if (mGraphView?.Graph == default || mTarget == default)
                return;

            var previousStartNode = mGraphView.Graph.StartNode;
            if (previousStartNode == mTarget)
                return;

            if (previousStartNode != default)
                NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mGraphView.Graph, mTarget, previousStartNode }, "Set Start Node");
            else
                NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mGraphView.Graph, mTarget }, "Set Start Node");

            mGraphView.Graph.SetStartNode(mTarget);
            mGraphView.RefreshStartNodeState(previousStartNode);
            mGraphView.SaveGraph();
        }

        private void ClearStartNode()
        {
            if (mGraphView?.Graph == default || mTarget == default)
                return;

            if (mGraphView.Graph.StartNode != mTarget)
                return;

            NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mGraphView.Graph, mTarget }, "Clear Start Node");
            mGraphView.Graph.ClearStartNode();
            mGraphView.RefreshStartNodeState(mTarget);
            mGraphView.SaveGraph();
        }

        private static void AppendCustomContextMenu(ContextualMenuPopulateEvent evt, object target)
        {
            var items = NodeEditorUtility.GetContextMenuMethods(target);
            if (items.Length == 0) return;

            evt.menu.AppendSeparator();
            for (int i = 0; i < items.Length; i++)
            {
                var method = items[i].Value;
                var menu = items[i].Key;
                evt.menu.AppendAction(menu.menuItem, _ => method.Invoke(target, null));
            }
        }
    }
}
