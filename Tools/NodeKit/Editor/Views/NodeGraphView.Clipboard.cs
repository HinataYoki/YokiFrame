using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public partial class NodeGraphView
    {
        private static Node[] sCopyBuffer;
        private Vector2 mLastMouseGraphPosition;
        private bool mHasMouseGraphPosition;

        public void CopySelectionNodes()
        {
            sCopyBuffer = selection.OfType<NodeView>()
                .Select(view => view.Target)
                .Where(node => node != default && node.Graph == Graph)
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
                .Where(node => node != default && node.Graph == Graph)
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

        private void InsertDuplicateNodes(Node[] nodes, Vector2 topLeft)
        {
            if (nodes == default || nodes.Length == 0 || Graph == default) return;

            var validNodes = nodes.Where(node => node != default && node.Graph == Graph).ToArray();
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

                var copy = Graph.CopyNode(sourceNode);
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

        private void OnMouseMove(MouseMoveEvent evt)
        {
            mLastMouseGraphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);
            mHasMouseGraphPosition = true;
        }

    }
}
