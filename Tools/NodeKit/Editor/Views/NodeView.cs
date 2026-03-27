using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using GVNode = UnityEditor.Experimental.GraphView.Node;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 节点视图
    /// </summary>
    public partial class NodeView : GVNode
    {
        private Node mTarget;
        private NodeEditorBase mNodeEditor;
        private NodeGraphView mGraphView;
        private Dictionary<string, PortView> mPortViews = new();

        public Node Target => mTarget;
        public NodeEditorBase NodeEditor => mNodeEditor;
        public NodeGraphView GraphView => mGraphView;

        public NodeView() 
        {
            AddToClassList("yoki-node");
            
            // 直接设置圆角样式覆盖基类默认样式
            style.borderTopLeftRadius = 12;
            style.borderTopRightRadius = 12;
            style.borderBottomLeftRadius = 12;
            style.borderBottomRightRadius = 12;
            style.overflow = Overflow.Hidden;
        }

        /// <summary>
        /// 初始化节点视图
        /// </summary>
        public void Initialize(Node target, NodeGraphView graphView)
        {
            mTarget = target;
            mGraphView = graphView;

            // 创建节点编辑器
            var editorType = NodeReflection.GetNodeEditorType(target.GetType());
            mNodeEditor = Activator.CreateInstance(editorType) as NodeEditorBase;
            mNodeEditor.Initialize(target, graphView);
            mNodeEditor.SetNodeView(this);

            // 设置位置
            SetPosition(new Rect(target.Position, Vector2.zero));

            // 设置样式
            style.width = mNodeEditor.GetWidth();
            var tint = mNodeEditor.GetTint();
            style.backgroundColor = new StyleColor(tint);

            // 构建 UI
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
        }

        private void BuildBody()
        {
            var body = extensionContainer;
            body.Clear();
            body.AddToClassList("yoki-node__body");
            mNodeEditor.OnBodyGUI(body);
        }

        /// <summary>
        /// 获取端口视图
        /// </summary>
        public PortView GetPortView(NodePort port)
        {
            if (port == default) return null;
            return mPortViews.TryGetValue(port.FieldName, out var view) ? view : null;
        }

        /// <summary>
        /// 获取端口视图
        /// </summary>
        public PortView GetPortView(string fieldName)
        {
            return mPortViews.TryGetValue(fieldName, out var view) ? view : null;
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            if (mTarget != default)
            {
                NodeEditorUtility.RecordUndo(mTarget, "Move Node");
                mTarget.Position = new Vector2(newPos.x, newPos.y);
                NodeEditorUtility.SetDirty(mTarget);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Delete", _ => mGraphView.DeleteNode(this));
            evt.menu.AppendAction("Duplicate", _ => mGraphView.DuplicateNode(this));
            evt.menu.AppendSeparator();
            mNodeEditor.BuildContextMenu(evt);
        }
    }
}
