using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 连线视图
    /// </summary>
    public class EdgeView : Edge
    {
        private NodePort mOutputPort;
        private NodePort mInputPort;

        public NodePort OutputPort => mOutputPort;
        public NodePort InputPort => mInputPort;

        public EdgeView() 
        {
            AddToClassList("yoki-edge");
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenu);
        }

        /// <summary>
        /// 设置端口引用
        /// </summary>
        public void SetPorts(NodePort outputPort, NodePort inputPort)
        {
            mOutputPort = outputPort;
            mInputPort = inputPort;
        }

        /// <summary>
        /// 创建连线视图
        /// </summary>
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

            return edge;
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Delete", _ =>
            {
                var graphView = GetFirstAncestorOfType<NodeGraphView>();
                if (graphView == default) return;
                graphView.DeleteElements(new[] { this });
            });
            evt.StopPropagation();
        }
    }
}
