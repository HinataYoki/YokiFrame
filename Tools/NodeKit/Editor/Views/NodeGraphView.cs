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
        private static Node[] sCopyBuffer;
        private bool mSuppressGraphViewChanges;
        private readonly GraphGridBackground mGridBackground;
        private NodeGraph mGraph;
        private NodeGraphEditorBase mGraphEditor;
        private Dictionary<Node, NodeView> mNodeViews = new();
        private NodeSearchWindow mSearchWindow;
        private Vector2 mLastMouseGraphPosition;
        private bool mHasMouseGraphPosition;

        public NodeGraph Graph => mGraph;
        public NodeGraphEditorBase GraphEditor => mGraphEditor;
        internal bool SuppressGraphViewChanges => mSuppressGraphViewChanges;

        public NodeGraphView()
        {
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            SetupZoom(NodePreferences.MinZoom, NodePreferences.MaxZoom);

            mGridBackground = new GraphGridBackground(this);
            Insert(0, mGridBackground);
            mGridBackground.StretchToParentSize();

            AddToClassList("yoki-graph-view");
            graphViewChanged = OnGraphViewChanged;
            viewTransformChanged += _ => mGridBackground.MarkDirtyRepaint();
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
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

        public void CopySelectionNodes()
        {
            sCopyBuffer = selection.OfType<NodeView>()
                .Select(view => view.Target)
                .Where(node => node != default && node.Graph == mGraph)
                .ToArray();
        }

        public void CopyNode(NodeView nodeView)
        {
            if (nodeView == default || nodeView.Target == default) return;
            sCopyBuffer = new[] { nodeView.Target };
        }

        public void PasteNodes(Vector2 position)
        {
            InsertDuplicateNodes(sCopyBuffer, position);
        }

        public Vector2 GetPreferredPastePosition()
        {
            if (mHasMouseGraphPosition)
                return mLastMouseGraphPosition;

            return contentViewContainer.WorldToLocal(contentRect.center);
        }

        public void DuplicateSelectionNodes()
        {
            var selectedNodes = selection.OfType<NodeView>()
                .Select(view => view.Target)
                .Where(node => node != default && node.Graph == mGraph)
                .ToArray();
            if (selectedNodes.Length == 0) return;

            var topLeft = selectedNodes
                .Select(node => node.Position)
                .Aggregate((a, b) => new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)));

            InsertDuplicateNodes(selectedNodes, topLeft + new Vector2(30f, 30f));
        }

        public bool HasCopyBuffer()
        {
            return sCopyBuffer != default && sCopyBuffer.Length > 0;
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

        private void InsertDuplicateNodes(Node[] nodes, Vector2 topLeft)
        {
            if (nodes == default || nodes.Length == 0 || mGraph == default) return;

            var validNodes = nodes.Where(node => node != default && node.Graph == mGraph).ToArray();
            if (validNodes.Length == 0) return;

            var topLeftNode = validNodes
                .Select(node => node.Position)
                .Aggregate((a, b) => new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)));

            var offset = topLeft - topLeftNode;
            var substitutes = new Dictionary<Node, Node>();
            var createdViews = new List<NodeView>();

            for (int i = 0; i < validNodes.Length; i++)
            {
                var sourceNode = validNodes[i];
                if (HasDisallowMultipleNodes(sourceNode.GetType())) continue;

                var copy = mGraph.CopyNode(sourceNode);
                if (copy == default) continue;

                copy.Position = sourceNode.Position + offset;
                substitutes[sourceNode] = copy;
                var view = CreateNodeView(copy);
                if (view != default) createdViews.Add(view);
            }

            foreach (var pair in substitutes)
            {
                var original = pair.Key;
                var duplicate = pair.Value;
                foreach (var port in original.Ports)
                {
                    if (!port.IsOutput) continue;
                    var duplicatePort = duplicate.GetOutputPort(port.FieldName);
                    if (duplicatePort == default) continue;

                    for (int i = 0; i < port.ConnectionCount; i++)
                    {
                        var targetPort = port.GetConnection(i);
                        if (targetPort == default) continue;
                        if (!substitutes.TryGetValue(targetPort.Node, out var duplicateTargetNode)) continue;

                        var duplicateInputPort = duplicateTargetNode.GetInputPort(targetPort.FieldName);
                        if (duplicateInputPort == default) continue;
                        if (!duplicatePort.IsConnectedTo(duplicateInputPort))
                        {
                            duplicatePort.Connect(duplicateInputPort);
                            var sourceReroutePoints = port.GetReroutePoints(i);
                            var duplicateConnectionIndex = duplicatePort.GetConnectionIndex(duplicateInputPort);
                            var duplicateReroutePoints = duplicatePort.GetReroutePoints(duplicateConnectionIndex);
                            if (sourceReroutePoints != default && duplicateReroutePoints != default)
                                duplicateReroutePoints.AddRange(sourceReroutePoints);
                        }
                    }
                }
            }

            ClearSelection();
            for (int i = 0; i < createdViews.Count; i++)
            {
                var view = createdViews[i];
                if (view == default)
                    continue;

                RefreshNodeView(view.Target);
                AddToSelection(view);
            }

            RefreshConnections(validNodes);
            RefreshConnections(substitutes.Values.ToArray());
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

        private void OnMouseMove(MouseMoveEvent evt)
        {
            mLastMouseGraphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);
            mHasMouseGraphPosition = true;
        }

        private sealed class GraphGridBackground : ImmediateModeElement
        {
            private readonly NodeGraphView mOwner;

            public GraphGridBackground(NodeGraphView owner)
            {
                mOwner = owner;
                pickingMode = PickingMode.Ignore;
            }

            protected override void ImmediateRepaint()
            {
                var rect = contentRect;
                EditorGUI.DrawRect(rect, NodePreferences.GridBgColor);

                Handles.BeginGUI();
                var oldColor = Handles.color;

                DrawGrid(rect, 20f, NodePreferences.GridLineColor);
                DrawGrid(rect, 100f, NodePreferences.GridMajorLineColor);

                Handles.color = oldColor;
                Handles.EndGUI();
            }

            private void DrawGrid(Rect rect, float spacing, Color color)
            {
                var offset = mOwner.contentViewContainer.transform.position;
                var scale = mOwner.scale;
                float scaledSpacing = spacing * scale;
                if (scaledSpacing < 8f) return;

                Handles.color = color;

                float startX = Mathf.Repeat(offset.x, scaledSpacing);
                for (float x = startX; x < rect.width; x += scaledSpacing)
                    Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, rect.height));

                float startY = Mathf.Repeat(offset.y, scaledSpacing);
                for (float y = startY; y < rect.height; y += scaledSpacing)
                    Handles.DrawLine(new Vector3(0f, y), new Vector3(rect.width, y));
            }
        }
    }
}
