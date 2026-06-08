using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public partial class EdgeView : Edge
    {
        private readonly EdgeImmediateElement mOverlay;
        private readonly VisualElement mRerouteLayer;
        private readonly List<RerouteHandle> mRerouteHandles = new();
        private RerouteHandle mActiveDragHandle;
        private NodePort mOutputPort;
        private NodePort mInputPort;

        public NodePort OutputPort => mOutputPort;
        public NodePort InputPort => mInputPort;

        public EdgeView()
        {
            AddToClassList("yoki-edge");
            edgeControl.visible = true;

            mOverlay = new EdgeImmediateElement(DrawOverlay)
            {
                pickingMode = PickingMode.Ignore
            };
            mOverlay.StretchToParentSize();
            Add(mOverlay);

            mRerouteLayer = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            mRerouteLayer.StretchToParentSize();
            Add(mRerouteLayer);

            RegisterCallback<GeometryChangedEvent>(_ => RefreshRerouteHandles());
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenu);
        }

        public void SetPorts(NodePort outputPort, NodePort inputPort)
        {
            mOutputPort = outputPort;
            mInputPort = inputPort;
            RefreshEdgeControl();
            RefreshRerouteHandles();
            MarkDirtyRepaint();
        }

        public static EdgeView Create(PortView outputPortView, PortView inputPortView)
        {
            var edge = new EdgeView
            {
                output = outputPortView,
                input = inputPortView
            };
            edge.SetPorts(outputPortView.NodePort, inputPortView.NodePort);

            outputPortView.Connect(edge);
            inputPortView.Connect(edge);
            edge.RefreshEdgeControl();
            return edge;
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            var points = GetRenderPoints();
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (DistanceToSegment(localPoint, points[i], points[i + 1]) <= 8f)
                    return true;
            }

            return false;
        }

        private void RefreshRerouteHandles()
        {
            var reroutePoints = GetReroutePoints();
            mRerouteLayer.Clear();
            mRerouteHandles.Clear();

            if (reroutePoints == default) return;

            for (int i = 0; i < reroutePoints.Count; i++)
            {
                var handle = new RerouteHandle(this, i);
                var localPoint = this.WorldToLocal(reroutePoints[i]);
                handle.style.left = localPoint.x - 5f;
                handle.style.top = localPoint.y - 5f;
                mRerouteLayer.Add(handle);
                mRerouteHandles.Add(handle);
            }
        }

        internal void DragReroute(int rerouteIndex, Vector2 worldPosition)
        {
            var graphView = GetFirstAncestorOfType<NodeGraphView>();
            if (graphView == default) return;

            int connectionIndex = mOutputPort.GetConnectionIndex(mInputPort);
            var points = mOutputPort.GetReroutePoints(connectionIndex);
            if (points == default || rerouteIndex < 0 || rerouteIndex >= points.Count) return;

            points[rerouteIndex] = worldPosition;
            RefreshRerouteHandles();
            MarkDirtyRepaint();
        }

        internal void CommitRerouteDrag()
        {
            var graphView = GetFirstAncestorOfType<NodeGraphView>();
            if (graphView == default) return;
            graphView.SaveGraph();
        }

        private void BeginHandleDrag(RerouteHandle handle)
        {
            mActiveDragHandle = handle;
            RegisterCallback<PointerMoveEvent>(OnHandlePointerMove);
            RegisterCallback<PointerUpEvent>(OnHandlePointerUp);
        }

        private void OnHandlePointerMove(PointerMoveEvent evt)
        {
            if (mActiveDragHandle == default) return;
            mActiveDragHandle.OnDragMove(evt);
            evt.StopPropagation();
        }

        private void OnHandlePointerUp(PointerUpEvent evt)
        {
            if (mActiveDragHandle == default) return;
            mActiveDragHandle.OnDragEnd(evt);
            mActiveDragHandle = null;
            UnregisterCallback<PointerMoveEvent>(OnHandlePointerMove);
            UnregisterCallback<PointerUpEvent>(OnHandlePointerUp);
            evt.StopPropagation();
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Add Reroute", _ =>
            {
                var graphView = GetFirstAncestorOfType<NodeGraphView>();
                if (graphView == default) return;

                int index = mOutputPort.GetConnectionIndex(mInputPort);
                if (index < 0) return;

                var points = mOutputPort.GetReroutePoints(index);
                if (points == default) return;

                NodeEditorUtility.RecordUndo(graphView.Graph, "Add Reroute");
                var worldPoint = this.LocalToWorld(evt.localMousePosition);
                int insertIndex = GetNearestSegmentInsertIndex(evt.localMousePosition);
                points.Insert(insertIndex, worldPoint);
                RefreshRerouteHandles();
                graphView.SaveGraph();
                MarkDirtyRepaint();
            });

            var rerouteIndex = FindHoveredRerouteIndex(evt.localMousePosition);
            if (rerouteIndex >= 0)
            {
                evt.menu.AppendAction("Remove Reroute", _ =>
                {
                    var graphView = GetFirstAncestorOfType<NodeGraphView>();
                    if (graphView == default) return;

                    int connectionIndex = mOutputPort.GetConnectionIndex(mInputPort);
                    var points = mOutputPort.GetReroutePoints(connectionIndex);
                    if (points == default || rerouteIndex >= points.Count) return;

                    NodeEditorUtility.RecordUndo(graphView.Graph, "Remove Reroute");
                    points.RemoveAt(rerouteIndex);
                    RefreshRerouteHandles();
                    graphView.SaveGraph();
                    MarkDirtyRepaint();
                });
            }

            evt.menu.AppendAction("Delete", _ =>
            {
                var graphView = GetFirstAncestorOfType<NodeGraphView>();
                if (graphView == default) return;
                graphView.DeleteElements(new[] { this });
            });
            evt.StopPropagation();
        }

        private int FindHoveredRerouteIndex(Vector2 localPoint)
        {
            var reroutePoints = GetReroutePoints();
            if (reroutePoints == default) return -1;

            for (int i = 0; i < reroutePoints.Count; i++)
            {
                var point = this.WorldToLocal(reroutePoints[i]);
                if (Vector2.Distance(point, localPoint) <= 6f)
                    return i;
            }

            return -1;
        }

        private int GetNearestSegmentInsertIndex(Vector2 localPoint)
        {
            var points = GetRenderPoints();
            if (points.Count < 2) return 0;

            int bestSegmentIndex = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                float distance = DistanceToSegment(localPoint, points[i], points[i + 1]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSegmentIndex = i;
                }
            }

            return Mathf.Clamp(bestSegmentIndex, 0, Mathf.Max(0, points.Count - 2));
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float denominator = Vector2.Dot(ab, ab);
            if (denominator <= Mathf.Epsilon) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
            var projection = a + ab * t;
            return Vector2.Distance(point, projection);
        }

        private sealed class RerouteHandle : VisualElement
        {
            private readonly EdgeView mEdgeView;
            private readonly int mIndex;
            private bool mDragging;
            private Vector2 mPointerOffset;

            public RerouteHandle(EdgeView edgeView, int index)
            {
                mEdgeView = edgeView;
                mIndex = index;
                AddToClassList("yoki-reroute");
                pickingMode = PickingMode.Position;

                RegisterCallback<PointerDownEvent>(OnPointerDown);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0) return;
                mDragging = true;
                var localPosition = new Vector2(evt.localPosition.x, evt.localPosition.y);
                mPointerOffset = localPosition - new Vector2(layout.width * 0.5f, layout.height * 0.5f);
                mEdgeView.BeginHandleDrag(this);
                evt.StopPropagation();
            }

            internal void OnDragMove(PointerMoveEvent evt)
            {
                if (!mDragging) return;
                var edgeLocal = mEdgeView.WorldToLocal(evt.position);
                var handleCenter = edgeLocal - mPointerOffset + new Vector2(layout.width * 0.5f, layout.height * 0.5f);
                var worldPosition = mEdgeView.LocalToWorld(handleCenter);
                mEdgeView.DragReroute(mIndex, worldPosition);
            }

            internal void OnDragEnd(PointerUpEvent evt)
            {
                if (!mDragging || evt.button != 0) return;
                mDragging = false;
                mEdgeView.CommitRerouteDrag();
            }
        }
    }
}
