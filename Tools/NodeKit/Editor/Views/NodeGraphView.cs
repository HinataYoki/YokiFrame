using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public partial class NodeGraphView : GraphView
    {
        private bool mSuppressGraphViewChanges;
        private bool mCtrlHeld;
        private NodeGraph mGraph;
        private NodeGraphEditorBase mGraphEditor;
        private Dictionary<Node, NodeView> mNodeViews = new();
        private NodeSearchWindow mSearchWindow;

        public NodeGraph Graph => mGraph;
        public NodeGraphEditorBase GraphEditor => mGraphEditor;
        internal bool SuppressGraphViewChanges => mSuppressGraphViewChanges;
        internal bool IsCtrlHeld => mCtrlHeld;

        public NodeGraphView()
        {
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            SetupZoom(NodePreferences.MinZoom, NodePreferences.MaxZoom);

            AddToClassList("yoki-graph-view");

            // Dot-grid overlay inside contentViewContainer.
            // The viewTransform (pan/zoom) on contentViewContainer automatically moves
            // the grid with the nodes. Large fixed size ensures coverage in all directions.
            var gridTex = NodeGridTexture.GetOrCreate();
            if (gridTex != default)
            {
                const float overlayExtent = 5000f;
                var gridOverlay = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        position = Position.Absolute,
                        width = overlayExtent * 2f,
                        height = overlayExtent * 2f,
                        left = -overlayExtent,
                        top = -overlayExtent,
                        backgroundImage = new StyleBackground(gridTex)
                    }
                };
                contentViewContainer.Insert(0, gridOverlay);
            }

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<KeyDownEvent>(OnCtrlKeyDown);
            RegisterCallback<KeyUpEvent>(OnCtrlKeyUp);
        }

        private void OnCtrlKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.LeftControl or KeyCode.RightControl)
                mCtrlHeld = true;
        }

        private void OnCtrlKeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode is KeyCode.LeftControl or KeyCode.RightControl)
                mCtrlHeld = false;
        }

        public void SetSearchWindow(NodeSearchWindow searchWindow)
        {
            mSearchWindow = searchWindow;
        }

        public void LoadGraph(NodeGraph graph)
        {
            if (graph == default) return;

            mSuppressGraphViewChanges = true;
            try
            {
                ClearGraph();
                mGraph = graph;

                var editorType = NodeReflection.GetGraphEditorType(graph.GetType());
                mGraphEditor = Activator.CreateInstance(editorType) as NodeGraphEditorBase;
                mGraphEditor.Initialize(graph, this);

                var nodes = graph.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node == default) continue;
                    CreateNodeView(node);
                }

                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node == default) continue;
                    CreateEdgeViews(node);
                }
            }
            finally
            {
                mSuppressGraphViewChanges = false;
            }
        }

        public void ClearGraph()
        {
            var elements = new List<GraphElement>(graphElements);
            mNodeViews.Clear();
            if (elements.Count > 0)
                DeleteElements(elements);
            mGraph = null;
            mGraphEditor = null;
        }

        public void SaveGraph()
        {
            if (mGraph == default) return;
            NodeEditorUtility.SaveAsset(mGraph);
        }

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

        public NodeView GetNodeView(Node node)
        {
            return mNodeViews.TryGetValue(node, out var view) ? view : null;
        }

        public void RefreshNodeView(Node node)
        {
            if (node == default || !mNodeViews.TryGetValue(node, out var nodeView))
                return;

            nodeView.RefreshContents();
        }

        public void RefreshStartNodeState(Node previousStartNode = null)
        {
            if (previousStartNode != default && mNodeViews.TryGetValue(previousStartNode, out var previousView))
                previousView.RefreshContents();

            var currentStartNode = mGraph?.StartNode;
            if (currentStartNode != default && currentStartNode != previousStartNode && mNodeViews.TryGetValue(currentStartNode, out var currentView))
                currentView.RefreshContents();
        }

        public void RefreshConnections(params Node[] nodes)
        {
            if (mGraph == default || nodes == default || nodes.Length == 0)
                return;

            var affectedNodes = new HashSet<Node>();
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != default)
                    affectedNodes.Add(nodes[i]);
            }

            if (affectedNodes.Count == 0)
                return;

            var elementsToRemove = new List<GraphElement>();
            foreach (var element in graphElements)
            {
                if (element is not EdgeView edgeView)
                    continue;

                if (affectedNodes.Contains(edgeView.OutputPort?.Node) || affectedNodes.Contains(edgeView.InputPort?.Node))
                    elementsToRemove.Add(edgeView);
            }

            if (elementsToRemove.Count > 0)
            {
                mSuppressGraphViewChanges = true;
                try
                {
                    DeleteElements(elementsToRemove);
                }
                finally
                {
                    mSuppressGraphViewChanges = false;
                }
            }

            var graphNodes = mGraph.Nodes;
            for (int i = 0; i < graphNodes.Count; i++)
            {
                var graphNode = graphNodes[i];
                if (graphNode == default)
                    continue;

                bool touchesAffectedNode = affectedNodes.Contains(graphNode);
                if (!touchesAffectedNode)
                {
                    foreach (var output in graphNode.Outputs)
                    {
                        for (int connectionIndex = 0; connectionIndex < output.ConnectionCount; connectionIndex++)
                        {
                            var targetPort = output.GetConnection(connectionIndex);
                            if (targetPort != default && affectedNodes.Contains(targetPort.Node))
                            {
                                touchesAffectedNode = true;
                                break;
                            }
                        }

                        if (touchesAffectedNode)
                            break;
                    }
                }

                if (touchesAffectedNode)
                    CreateEdgeViews(graphNode);
            }
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

                if (port is PortView portView && startPortView.NodePort.CanConnectTo(portView.NodePort))
                    result.Add(port);
            }
            return result;
        }

        public void HandleDropOutsidePort(Edge edge, Vector2 position)
        {
            if (!NodePreferences.DragToCreate || mSearchWindow == default) return;

            var sourcePortView = edge.output as PortView ?? edge.input as PortView;
            if (sourcePortView == default) return;

            var graphPosition = contentViewContainer.WorldToLocal(position);
            var screenPosition = GUIUtility.GUIToScreenPoint(position);

            mSearchWindow.SetMousePosition(graphPosition);
            mSearchWindow.SetCompatibility(sourcePortView.NodePort);
            SearchWindow.Open(new SearchWindowContext(screenPosition), mSearchWindow);
        }

        public void AutoConnect(Node node, NodePort sourcePort)
        {
            if (node == default || sourcePort == default) return;

            NodePort candidate = null;
            foreach (var port in node.Ports)
            {
                if (sourcePort.IsOutput && !port.IsInput) continue;
                if (sourcePort.IsInput && !port.IsOutput) continue;
                if (!sourcePort.CanConnectTo(port)) continue;
                candidate = port;
                break;
            }

            if (candidate == default) return;

            if (sourcePort.IsOutput) sourcePort.Connect(candidate);
            else candidate.Connect(sourcePort);

            RefreshNodeView(node);
            RefreshNodeView(sourcePort.Node);
            RefreshConnections(node, sourcePort.Node);
            SaveGraph();
        }

        public void MoveNodeToFront(NodeView nodeView)
        {
            if (nodeView == default) return;
            if (mGraph != default && nodeView.Target != default)
            {
                NodeEditorUtility.RecordUndo(new UnityEngine.Object[] { mGraph, nodeView.Target }, "Move Node To Front");
                if (mGraph.MoveNodeToFront(nodeView.Target))
                    NodeEditorUtility.SetDirty(mGraph);
            }

            RemoveElement(nodeView);
            AddElement(nodeView);
            AddToSelection(nodeView);
            SaveGraph();
        }

    }
}
