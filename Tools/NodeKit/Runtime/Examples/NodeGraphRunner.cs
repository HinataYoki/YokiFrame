using UnityEngine;

namespace YokiFrame.NodeKit.Examples
{
    /// <summary>
    /// 节点图运行器，用于测试图的执行
    /// </summary>
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

        /// <summary>
        /// 运行节点图
        /// </summary>
        [ContextMenu("Run Graph")]
        public void Run()
        {
            if (mGraph == default)
            {
                Debug.LogWarning("[NodeKit] No graph assigned to runner.");
                return;
            }

            Debug.Log($"[NodeKit] Running graph: {mGraph.name}");

            var nodes = mGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == default) continue;

                // 查找输出端口并获取值
                foreach (var port in node.Outputs)
                {
                    var value = node.GetValue(port);
                    Debug.Log($"[NodeKit] Node '{node.name}' Port '{port.FieldName}' = {value}");
                }
            }

            Debug.Log("[NodeKit] Graph execution completed.");
        }

        /// <summary>
        /// 获取指定类型的节点
        /// </summary>
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

        /// <summary>
        /// 获取所有指定类型的节点
        /// </summary>
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
