using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public class PortView : Port
    {
        private NodePort mNodePort;
        private NodeView mNodeView;

        public NodePort NodePort => mNodePort;
        public NodeView NodeView => mNodeView;

        private PortView(Orientation orientation, Direction direction, Capacity capacity, System.Type type)
            : base(orientation, direction, capacity, type) { }

        public static PortView Create(NodePort nodePort, NodeView nodeView)
        {
            var direction = nodePort.IsInput ? Direction.Input : Direction.Output;
            var capacity = nodePort.ConnectionType == ConnectionType.Multiple ? Capacity.Multi : Capacity.Single;

            var listener = new DefaultEdgeConnectorListener();
            var port = new PortView(Orientation.Horizontal, direction, capacity, nodePort.ValueType)
            {
                mNodePort = nodePort,
                mNodeView = nodeView,
                portName = nodePort.FieldName
            };

            port.m_EdgeConnector = new EdgeConnector<EdgeView>(listener);
            port.AddManipulator(port.m_EdgeConnector);

            port.AddToClassList("yoki-port");
            port.AddToClassList(nodePort.IsInput ? "yoki-port--input" : "yoki-port--output");

            var graphView = nodeView.GraphView;
            if (graphView != default)
                port.portColor = graphView.GraphEditor.GetPortColor(nodePort);

            if (NodePreferences.PortTooltips && graphView != default)
                port.tooltip = graphView.GraphEditor.GetPortTooltip(nodePort);

            port.RegisterCallback<ContextualMenuPopulateEvent>(port.OnContextMenu);
            port.RegisterCallback<MouseEnterEvent>(port.OnMouseEnter);
            return port;
        }

        public void RefreshColor()
        {
            var graphView = mNodeView.GraphView;
            if (graphView == default) return;
            portColor = graphView.GraphEditor.GetPortColor(mNodePort);
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            var graphView = mNodeView?.GraphView;
            if (graphView == default)
                return;

            tooltip = NodePreferences.PortTooltips
                ? graphView.GraphEditor.GetPortTooltip(mNodePort)
                : string.Empty;
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (mNodePort == default) return;

            if (mNodePort.IsConnected)
            {
                for (int i = 0; i < mNodePort.ConnectionCount; i++)
                {
                    var connectedPort = mNodePort.GetConnection(i);
                    if (connectedPort == default) continue;

                    evt.menu.AppendAction($"Disconnect/{connectedPort.Node.name}", _ =>
                    {
                        NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mNodePort.Node, connectedPort.Node, mNodeView.GraphView.Graph }, "Disconnect Port");
                        mNodePort.Disconnect(connectedPort);
                        mNodeView.GraphView.RefreshNodeView(mNodePort.Node);
                        mNodeView.GraphView.RefreshNodeView(connectedPort.Node);
                        mNodeView.GraphView.RefreshConnections(mNodePort.Node, connectedPort.Node);
                        mNodeView.GraphView.SaveGraph();
                    });
                }

                evt.menu.AppendAction("Disconnect All", _ =>
                {
                    NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mNodePort.Node, mNodeView.GraphView.Graph }, "Disconnect All");
                    var connectedNodes = new List<Node>();
                    for (int i = 0; i < mNodePort.ConnectionCount; i++)
                    {
                        var connectedPort = mNodePort.GetConnection(i);
                        if (connectedPort?.Node != default && !connectedNodes.Contains(connectedPort.Node))
                            connectedNodes.Add(connectedPort.Node);
                    }

                    mNodePort.ClearConnections();
                    mNodeView.GraphView.RefreshNodeView(mNodePort.Node);
                    for (int i = 0; i < connectedNodes.Count; i++)
                    {
                        mNodeView.GraphView.RefreshNodeView(connectedNodes[i]);
                    }
                    var refreshNodes = new Node[connectedNodes.Count + 1];
                    refreshNodes[0] = mNodePort.Node;
                    for (int i = 0; i < connectedNodes.Count; i++)
                        refreshNodes[i + 1] = connectedNodes[i];
                    mNodeView.GraphView.RefreshConnections(refreshNodes);
                    mNodeView.GraphView.SaveGraph();
                });
            }

            if (mNodePort.IsDynamic)
            {
                evt.menu.AppendAction("Remove Port", _ =>
                {
                    NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mNodePort.Node, mNodeView.GraphView.Graph }, "Remove Port");
                    mNodePort.Node.RemoveDynamicPort(mNodePort.FieldName);
                    mNodeView.RefreshAllPorts();
                    mNodeView.GraphView.SaveGraph();
                });
            }

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Create Node", _ =>
            {
                var graphView = mNodeView.GraphView;
                if (graphView == default) return;
                graphView.HandleDropOutsidePort(CreateEdgeStub(), evt.mousePosition);
            });
        }

        private Edge CreateEdgeStub()
        {
            var edge = new Edge();
            if (direction == Direction.Output) edge.output = this;
            else edge.input = this;
            return edge;
        }
    }

    internal class DefaultEdgeConnectorListener : IEdgeConnectorListener
    {
        private readonly GraphViewChange mGraphViewChange;
        private readonly List<Edge> mEdgesToCreate;
        private readonly List<GraphElement> mEdgesToDelete;

        public DefaultEdgeConnectorListener()
        {
            mEdgesToCreate = new List<Edge>();
            mEdgesToDelete = new List<GraphElement>();
            mGraphViewChange.edgesToCreate = mEdgesToCreate;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            if (edge.parent is NodeGraphView graphView)
            {
                graphView.HandleDropOutsidePort(edge, position);
                return;
            }

            if (edge.output is PortView outputPort)
                outputPort.NodeView.GraphView.HandleDropOutsidePort(edge, position);
            else if (edge.input is PortView inputPort)
                inputPort.NodeView.GraphView.HandleDropOutsidePort(edge, position);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            mEdgesToCreate.Clear();
            mEdgesToCreate.Add(edge);

            mEdgesToDelete.Clear();
            if (edge.input.capacity == Port.Capacity.Single)
            {
                foreach (var connection in edge.input.connections)
                {
                    if (connection != edge)
                        mEdgesToDelete.Add(connection);
                }
            }

            if (edge.output.capacity == Port.Capacity.Single)
            {
                foreach (var connection in edge.output.connections)
                {
                    if (connection != edge)
                        mEdgesToDelete.Add(connection);
                }
            }

            if (mEdgesToDelete.Count > 0)
                graphView.DeleteElements(mEdgesToDelete);

            var edgesToCreate = mEdgesToCreate;
            if (graphView.graphViewChanged != default)
                edgesToCreate = graphView.graphViewChanged(mGraphViewChange).edgesToCreate;

            for (int i = 0; i < edgesToCreate.Count; i++)
            {
                var createdEdge = edgesToCreate[i];
                graphView.AddElement(createdEdge);
                createdEdge.input.Connect(createdEdge);
                createdEdge.output.Connect(createdEdge);
            }
        }
    }
}
