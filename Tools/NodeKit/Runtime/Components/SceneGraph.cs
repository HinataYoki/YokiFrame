using UnityEngine;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 场景图组件，允许在场景中实例化节点图
    /// </summary>
    public class SceneGraph : MonoBehaviour
    {
        [SerializeField] private NodeGraph mGraph;

        public NodeGraph Graph
        {
            get => mGraph;
            set => mGraph = value;
        }
    }

    /// <summary>
    /// 泛型场景图组件
    /// </summary>
    public class SceneGraph<T> : SceneGraph where T : NodeGraph
    {
        public new T Graph
        {
            get => base.Graph as T;
            set => base.Graph = value;
        }
    }
}
