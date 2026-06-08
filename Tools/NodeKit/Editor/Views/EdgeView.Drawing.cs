using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    public partial class EdgeView
    {
        private sealed class EdgeImmediateElement : ImmediateModeElement
        {
            private readonly Action mDrawAction;

            public EdgeImmediateElement(Action drawAction)
            {
                mDrawAction = drawAction;
            }

            protected override void ImmediateRepaint()
            {
                mDrawAction?.Invoke();
            }
        }

        private void DrawOverlay()
        {
            if (OutputPort == default || InputPort == default) return;
            var graphView = GetFirstAncestorOfType<NodeGraphView>();
            var graphEditor = graphView?.GraphEditor;
            var reroutePoints = GetReroutePoints();
            bool needsOverlay = graphEditor?.GetNoodleStroke(OutputPort, InputPort) == NoodleStroke.Dashed
                || (reroutePoints != default && reroutePoints.Count > 0);
            if (!needsOverlay) return;

            var points = GetRenderPoints();
            if (points.Count < 2) return;

            var color = graphEditor?.GetPortColor(OutputPort) ?? Color.white;
            if (selected) color = NodePreferences.HighlightColor;

            Handles.BeginGUI();
            var previousColor = Handles.color;
            Handles.color = color;

            for (int i = 0; i < points.Count - 1; i++)
                DrawSegment(points[i], points[i + 1], color);

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private void DrawSegment(Vector2 from, Vector2 to, Color color)
        {
            var graphEditor = GetFirstAncestorOfType<NodeGraphView>()?.GraphEditor;
            switch (graphEditor?.GetNoodlePath(OutputPort, InputPort) ?? NodePreferences.NoodlePath)
            {
                case NoodlePath.Straight:
                    DrawLineSegment(from, to, color);
                    break;
                case NoodlePath.Angled:
                    DrawAngledSegment(from, to, color);
                    break;
                default:
                    var tangentDistance = Mathf.Max(40f, Mathf.Abs(to.x - from.x) * 0.35f);
                    var fromTangent = from + Vector2.right * tangentDistance;
                    var toTangent = to + Vector2.left * tangentDistance;
                    DrawBezierSegment(from, to, fromTangent, toTangent, color);
                    break;
            }
        }

        private void DrawAngledSegment(Vector2 from, Vector2 to, Color color)
        {
            var midX = (from.x + to.x) * 0.5f;
            var cornerA = new Vector2(midX, from.y);
            var cornerB = new Vector2(midX, to.y);
            DrawLineSegment(from, cornerA, color);
            DrawLineSegment(cornerA, cornerB, color);
            DrawLineSegment(cornerB, to, color);
        }

        private void DrawLineSegment(Vector2 from, Vector2 to, Color color)
        {
            var graphEditor = GetFirstAncestorOfType<NodeGraphView>()?.GraphEditor;
            if ((graphEditor?.GetNoodleStroke(OutputPort, InputPort) ?? NodePreferences.NoodleStroke) == NoodleStroke.Dashed)
            {
                Handles.DrawDottedLine(from, to, 6f);
                return;
            }

            Handles.DrawAAPolyLine(graphEditor?.GetNoodleThickness(OutputPort, InputPort) ?? NodePreferences.NoodleThickness, from, to);
        }

        private void DrawBezierSegment(Vector2 from, Vector2 to, Vector2 fromTangent, Vector2 toTangent, Color color)
        {
            var graphEditor = GetFirstAncestorOfType<NodeGraphView>()?.GraphEditor;
            var noodleStroke = graphEditor?.GetNoodleStroke(OutputPort, InputPort) ?? NodePreferences.NoodleStroke;
            var noodleThickness = graphEditor?.GetNoodleThickness(OutputPort, InputPort) ?? NodePreferences.NoodleThickness;
            if (noodleStroke != NoodleStroke.Dashed)
            {
                Handles.DrawBezier(from, to, fromTangent, toTangent, color, null, noodleThickness);
                return;
            }

            const int segmentCount = 20;
            var previous = from;
            for (int i = 1; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                var point = EvaluateCubicBezier(from, fromTangent, toTangent, to, t);
                Handles.DrawDottedLine(previous, point, 6f);
                previous = point;
            }
        }

        private static Vector2 EvaluateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        private List<Vector2> GetRenderPoints()
        {
            var points = new List<Vector2>();
            if (output == default || input == default) return points;

            points.Add(this.WorldToLocal(output.worldBound.center));
            var reroutePoints = GetReroutePoints();
            if (reroutePoints != default)
            {
                for (int i = 0; i < reroutePoints.Count; i++)
                    points.Add(this.WorldToLocal(reroutePoints[i]));
            }
            points.Add(this.WorldToLocal(input.worldBound.center));
            return points;
        }

        private List<Vector2> GetReroutePoints()
        {
            if (OutputPort == default || InputPort == default) return null;
            int index = OutputPort.GetConnectionIndex(InputPort);
            return OutputPort.GetReroutePoints(index);
        }

        private void RefreshEdgeControl()
        {
            if (output == default || input == default)
                return;

            var graphView = GetFirstAncestorOfType<NodeGraphView>();
            var color = graphView?.GraphEditor?.GetPortColor(OutputPort) ?? Color.white;
            if ((graphView?.GraphEditor?.GetNoodleStroke(OutputPort, InputPort) ?? NodePreferences.NoodleStroke) == NoodleStroke.Dashed)
                color.a = 0f;
            edgeControl.inputColor = color;
            edgeControl.outputColor = color;
        }
    }
}
