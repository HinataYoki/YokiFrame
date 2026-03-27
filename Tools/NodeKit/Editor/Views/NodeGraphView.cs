using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 节点图视图
    /// </summary>
    public partial class NodeGraphView : GraphView
    {
        private NodeGraph mGraph;
        private NodeGraphEditorBase mGraphEditor;
        private Dictionary<Node, NodeView> mNodeViews = new();
        private NodeSearchWindow mSearchWindow;

        public NodeGraph Graph => mGraph;
        public NodeGraphEditorBase GraphEditor => mGraphEditor;

        public NodeGraphView()
        {
            // 添加操作器
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // 设置缩放范围
            SetupZoom(NodePreferences.MinZoom, NodePreferences.MaxZoom);

            // 添加网格背景
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // 添加样式
            AddToClassList("yoki-graph-view");

            // 注册回调
            graphViewChanged = OnGraphViewChanged;
        }

        /// <summary>
        /// 加载节点图
        /// </summary>
        public void LoadGraph(NodeGraph graph)
        {
            if (graph == default) return;

            ClearGraph();
            mGraph = graph;

            // 创建图编辑器
            var editorType = NodeReflection.GetGraphEditorType(graph.GetType());
            mGraphEditor = Activator.CreateInstance(editorType) as NodeGraphEditorBase;
            mGraphEditor.Initialize(graph, this);

            // 创建节点视图
            var nodes = graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == default) continue;
                CreateNodeView(node);
            }

            // 创建连线视图
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == default) continue;
                CreateEdgeViews(node);
            }
        }

        /// <summary>
        /// 清空图
        /// </summary>
        public void ClearGraph()
        {
            mGraph = null;
            mGraphEditor = null;
            mNodeViews.Clear();
            DeleteElements(graphElements);
        }

        /// <summary>
        /// 保存图
        /// </summary>
        public void SaveGraph()
        {
            if (mGraph == default) return;
            NodeEditorUtility.SaveAsset(mGraph);
        }

        /// <summary>
        /// 创建节点视图
        /// </summary>
        public NodeView CreateNodeView(Node node)
        {
            if (node == default || mNodeViews.ContainsKey(node)) return null;

            var nodeView = new NodeView();
            nodeView.Initialize(node, this);
            mNodeViews[node] = nodeView;
            AddElement(nodeView);
            return nodeView;
        }

        private void CreateEdgeViews(Node node)
        {
            if (!mNodeViews.TryGetValue(node, out var nodeView)) return;

            foreach (var port in node.Outputs)
            {
                var outputPortView = nodeView.GetPortView(port);
                if (outputPortView == default) continue;

                for (int i = 0; i < port.ConnectionCount; i++)
                {
                    var conn = port.Connections[i];
                    if (conn.Node == default) continue;
                    if (!mNodeViews.TryGetValue(conn.Node, out var targetNodeView)) continue;

                    var inputPortView = targetNodeView.GetPortView(conn.FieldName);
                    if (inputPortView == default) continue;

                    var edge = EdgeView.Create(outputPortView, inputPortView);
                    AddElement(edge);
                }
            }
        }

        /// <summary>
        /// 获取节点视图
        /// </summary>
        public NodeView GetNodeView(Node node)
        {
            return mNodeViews.TryGetValue(node, out var view) ? view : null;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = new List<Port>();
            var startPortView = startPort as PortView;
            if (startPortView == default) return result;

            foreach (var port in ports)
            {
                if (port == startPort) continue;
                if (port.node == startPort.node) continue;
                if (port.direction == startPort.direction) continue;

                if (port is PortView portView)
                {
                    if (startPortView.NodePort.CanConnectTo(portView.NodePort))
                        result.Add(port);
                }
            }
            return result;
        }
    }
}
