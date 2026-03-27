using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 图编辑器基类
    /// </summary>
    public class NodeGraphEditorBase
    {
        private NodeGraph mTarget;
        private NodeGraphView mGraphView;

        public NodeGraph Target => mTarget;
        public NodeGraphView GraphView => mGraphView;

        /// <summary>
        /// 初始化编辑器
        /// </summary>
        internal void Initialize(NodeGraph target, NodeGraphView graphView)
        {
            mTarget = target;
            mGraphView = graphView;
            OnEnable();
        }

        /// <summary>
        /// 编辑器启用回调
        /// </summary>
        protected virtual void OnEnable() { }

        /// <summary>
        /// 创建节点
        /// </summary>
        public virtual Node CreateNode(Type type, Vector2 position)
        {
            var node = mTarget.AddNode(type);
            if (node != default)
                node.Position = position;
            return node;
        }

        /// <summary>
        /// 检查是否可以连接
        /// </summary>
        public virtual bool CanConnect(NodePort output, NodePort input)
        {
            if (output == default || input == default) return false;
            return output.CanConnectTo(input);
        }

        /// <summary>
        /// 构建右键菜单
        /// </summary>
        public virtual void BuildContextMenu(ContextualMenuPopulateEvent evt) { }

        /// <summary>
        /// 获取端口颜色
        /// </summary>
        public virtual Color GetPortColor(NodePort port)
        {
            if (port == default) return Color.gray;
            return NodeEditorUtility.GetTypeColor(port.ValueType);
        }

        /// <summary>
        /// 获取类型颜色
        /// </summary>
        public virtual Color GetTypeColor(Type type) => NodeEditorUtility.GetTypeColor(type);
    }
}
