using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 端口视图
    /// </summary>
    public class PortView : Port
    {
        private NodePort mNodePort;
        private NodeView mNodeView;

        public NodePort NodePort => mNodePort;
        public NodeView NodeView => mNodeView;

        private PortView(Orientation orientation, Direction direction, Capacity capacity, System.Type type)
            : base(orientation, direction, capacity, type) { }

        /// <summary>
        /// 创建端口视图
        /// </summary>
        public static PortView Create(NodePort nodePort, NodeView nodeView)
        {
            var direction = nodePort.IsInput ? Direction.Input : Direction.Output;
            var capacity = nodePort.ConnectionType == ConnectionType.Multiple 
                ? Capacity.Multi 
                : Capacity.Single;

            // 使用默认 EdgeConnectorListener
            var listener = new DefaultEdgeConnectorListener();
            var port = new PortView(Orientation.Horizontal, direction, capacity, nodePort.ValueType)
            {
                mNodePort = nodePort,
                mNodeView = nodeView,
                portName = nodePort.FieldName
            };

            // 设置 EdgeConnector 以支持拖拽连线
            port.m_EdgeConnector = new EdgeConnector<EdgeView>(listener);
            port.AddManipulator(port.m_EdgeConnector);

            port.AddToClassList("yoki-port");
            port.AddToClassList(nodePort.IsInput ? "yoki-port--input" : "yoki-port--output");

            // 设置端口颜色
            var graphView = nodeView.GraphView;
            if (graphView != default)
            {
                var color = graphView.GraphEditor.GetPortColor(nodePort);
                port.portColor = color;
            }

            // 设置提示
            if (NodePreferences.PortTooltips)
            {
                var typeName = nodePort.ValueType == default ? "object" : nodePort.ValueType.Name;
                port.tooltip = $"{nodePort.FieldName} ({typeName})";
            }

            // 注册右键菜单
            port.RegisterCallback<ContextualMenuPopulateEvent>(port.OnContextMenu);

            return port;
        }

        /// <summary>
        /// 刷新端口颜色
        /// </summary>
        public void RefreshColor()
        {
            var graphView = mNodeView.GraphView;
            if (graphView == default) return;
            portColor = graphView.GraphEditor.GetPortColor(mNodePort);
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (mNodePort == default) return;

            // 断开所有连接
            if (mNodePort.IsConnected)
            {
                evt.menu.AppendAction("Disconnect All", _ =>
                {
                    NodeEditorUtility.RecordUndo(mNodePort.Node, "Disconnect All");
                    mNodePort.ClearConnections();
                    mNodeView.GraphView.LoadGraph(mNodeView.GraphView.Graph);
                });
            }

            // 动态端口可移除
            if (mNodePort.IsDynamic)
            {
                evt.menu.AppendAction("Remove Port", _ =>
                {
                    NodeEditorUtility.RecordUndo(mNodePort.Node, "Remove Port");
                    mNodePort.Node.RemoveDynamicPort(mNodePort.FieldName);
                    mNodeView.RefreshAllPorts();
                });
            }
        }
    }

    /// <summary>
    /// 默认边连接监听器
    /// </summary>
    internal class DefaultEdgeConnectorListener : IEdgeConnectorListener
    {
        private GraphViewChange mGraphViewChange;
        private System.Collections.Generic.List<Edge> mEdgesToCreate;
        private System.Collections.Generic.List<GraphElement> mEdgesToDelete;

        public DefaultEdgeConnectorListener()
        {
            mEdgesToCreate = new();
            mEdgesToDelete = new();
            mGraphViewChange.edgesToCreate = mEdgesToCreate;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position) { }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            mEdgesToCreate.Clear();
            mEdgesToCreate.Add(edge);

            mEdgesToDelete.Clear();
            if (edge.input.capacity == Port.Capacity.Single)
            {
                foreach (var conn in edge.input.connections)
                {
                    if (conn != edge)
                        mEdgesToDelete.Add(conn);
                }
            }
            if (edge.output.capacity == Port.Capacity.Single)
            {
                foreach (var conn in edge.output.connections)
                {
                    if (conn != edge)
                        mEdgesToDelete.Add(conn);
                }
            }

            if (mEdgesToDelete.Count > 0)
                graphView.DeleteElements(mEdgesToDelete);

            var edgesToCreate = mEdgesToCreate;
            if (graphView.graphViewChanged != null)
                edgesToCreate = graphView.graphViewChanged(mGraphViewChange).edgesToCreate;

            for (int i = 0; i < edgesToCreate.Count; i++)
            {
                var e = edgesToCreate[i];
                graphView.AddElement(e);
                edge.input.Connect(e);
                edge.output.Connect(e);
            }
        }
    }
}
