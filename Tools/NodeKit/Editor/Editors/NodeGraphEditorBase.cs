using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public class NodeGraphEditorBase
    {
        private NodeGraph mTarget;
        private NodeGraphView mGraphView;

        public NodeGraph Target => mTarget;
        public NodeGraphView GraphView => mGraphView;

        internal void Initialize(NodeGraph target, NodeGraphView graphView)
        {
            mTarget = target;
            mGraphView = graphView;
            OnEnable();
        }

        protected virtual void OnEnable() { }

        public virtual void OnGUI() { }

        public virtual void OnOpen() { }

        public virtual void OnWindowFocus() { }

        public virtual void OnWindowFocusLost() { }

        public virtual void OnDropObjects(UnityEngine.Object[] objects) { }

        public virtual Node CreateNode(Type type, Vector2 position)
        {
            var node = mTarget.AddNode(type);
            if (node != default)
            {
                node.Position = position;
                if (string.IsNullOrWhiteSpace(node.name))
                    node.name = NodeEditorUtility.NodeDefaultName(type);
            }

            return node;
        }

        public virtual bool CanConnect(NodePort output, NodePort input)
        {
            if (output == default || input == default) return false;
            return output.CanConnectTo(input);
        }

        public virtual void BuildContextMenu(ContextualMenuPopulateEvent evt) { }

        public virtual string GetNodeMenuName(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(CreateNodeMenuAttribute), true).FirstOrDefault() as CreateNodeMenuAttribute;
            return string.IsNullOrWhiteSpace(attr?.MenuName) ? NodeEditorUtility.NodeDefaultPath(type) : attr.MenuName;
        }

        public virtual int GetNodeMenuOrder(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(CreateNodeMenuAttribute), true).FirstOrDefault() as CreateNodeMenuAttribute;
            return attr?.Order ?? 0;
        }

        public virtual Color GetPortColor(NodePort port)
        {
            if (port == default) return Color.gray;
            return NodeEditorUtility.GetTypeColor(port.ValueType);
        }

        public virtual NoodlePath GetNoodlePath(NodePort output, NodePort input)
        {
            return NodePreferences.NoodlePath;
        }

        public virtual NoodleStroke GetNoodleStroke(NodePort output, NodePort input)
        {
            return NodePreferences.NoodleStroke;
        }

        public virtual float GetNoodleThickness(NodePort output, NodePort input)
        {
            return NodePreferences.NoodleThickness;
        }

        public virtual string GetPortTooltip(NodePort port)
        {
            if (port == default) return string.Empty;
            var tooltip = NodeEditorUtility.PrettyName(port.ValueType);
            if (!port.IsOutput) return tooltip;

            var value = port.Node.GetValue(port);
            return $"{tooltip} = {(value != default ? value : "null")}";
        }

        public virtual bool CanRemove(Node node) => mTarget != default && mTarget.CanRemoveNode(node);

        public virtual void RemoveNode(Node node)
        {
            if (!CanRemove(node)) return;
            mTarget.RemoveNode(node);
        }

        public virtual Color GetTypeColor(Type type) => NodeEditorUtility.GetTypeColor(type);
    }
}
