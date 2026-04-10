using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    public class NodeGraphRunner : MonoBehaviour
    {
        [SerializeField] private NodeGraph mGraph;
        [SerializeField] private bool mRunOnStart = true;

        public NodeGraph Graph
        {
            get => mGraph;
            set => mGraph = value;
        }

        private void Start()
        {
            if (mRunOnStart && mGraph != default)
                Run();
        }

        [ContextMenu("Run Graph")]
        public void Run()
        {
            if (mGraph == default)
            {
                Debug.LogWarning("[NodeKit] No graph assigned to runner.");
                return;
            }

            Debug.Log($"[NodeKit] Running graph: {mGraph.name}");

            var startNode = mGraph.StartNode;
            if (startNode != default)
            {
                Debug.Log($"[NodeKit] Start node: {startNode.name}");
                foreach (var port in startNode.Outputs)
                {
                    var value = startNode.GetValue(port);
                    Debug.Log($"[NodeKit] Node '{startNode.name}' Port '{port.FieldName}' = {value}");
                }
            }

            var nodes = mGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == default || node == startNode) continue;

                foreach (var port in node.Outputs)
                {
                    var value = node.GetValue(port);
                    Debug.Log($"[NodeKit] Node '{node.name}' Port '{port.FieldName}' = {value}");
                }
            }

            Debug.Log("[NodeKit] Graph execution completed.");
        }

        public T GetNode<T>() where T : Node
        {
            if (mGraph == default) return null;
            var nodes = mGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is T t) return t;
            }
            return null;
        }

        public void GetNodes<T>(System.Collections.Generic.List<T> result) where T : Node
        {
            result.Clear();
            if (mGraph == default) return;
            var nodes = mGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is T t) result.Add(t);
            }
        }
    }
}
