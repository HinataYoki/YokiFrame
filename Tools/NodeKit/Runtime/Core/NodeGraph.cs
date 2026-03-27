using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 节点图基类
    /// </summary>
    public abstract class NodeGraph : ScriptableObject
    {
        [SerializeField] protected List<Node> mNodes = new();

        public IReadOnlyList<Node> Nodes => mNodes;

        /// <summary>
        /// 添加节点
        /// </summary>
        public T AddNode<T>() where T : Node => AddNode(typeof(T)) as T;

        /// <summary>
        /// 添加节点
        /// </summary>
        public virtual Node AddNode(Type type)
        {
            if (!typeof(Node).IsAssignableFrom(type))
            {
                Debug.LogError($"[NodeKit] Type {type} is not a Node");
                return null;
            }

            var node = CreateInstance(type) as Node;
            if (node == default) return null;

            node.Graph = this;
            node.name = type.Name;
            node.UpdatePorts();
            mNodes.Add(node);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
#endif
            return node;
        }

        /// <summary>
        /// 移除节点
        /// </summary>
        public virtual void RemoveNode(Node node)
        {
            if (node == default) return;
            node.ClearAllConnections();
            mNodes.Remove(node);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
#endif
            DestroyImmediate(node, true);
        }

        /// <summary>
        /// 复制节点
        /// </summary>
        public virtual Node CopyNode(Node original)
        {
            if (original == default) return null;
            var copy = Instantiate(original);
            copy.Graph = this;
            copy.name = original.name;
            copy.ClearAllConnections();
            mNodes.Add(copy);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(copy, this);
#endif
            return copy;
        }

        /// <summary>
        /// 深拷贝整个图
        /// </summary>
        public virtual NodeGraph Copy()
        {
            var copy = Instantiate(this);
            copy.name = name + " (Copy)";

            // 建立原节点到新节点的映射
            var nodeMap = new Dictionary<Node, Node>();
            for (int i = 0; i < mNodes.Count; i++)
            {
                var original = mNodes[i];
                var newNode = copy.mNodes[i];
                nodeMap[original] = newNode;
                newNode.Graph = copy;
            }

            // 重建连接
            for (int i = 0; i < mNodes.Count; i++)
            {
                var original = mNodes[i];
                var newNode = copy.mNodes[i];
                foreach (var port in original.Ports)
                {
                    if (!port.IsOutput) continue;
                    var newPort = newNode.GetPort(port.FieldName);
                    if (newPort == default) continue;

                    for (int j = 0; j < port.ConnectionCount; j++)
                    {
                        var conn = port.Connections[j];
                        if (conn.Node == default) continue;
                        if (!nodeMap.TryGetValue(conn.Node, out var targetNode)) continue;
                        var targetPort = targetNode.GetPort(conn.FieldName);
                        if (targetPort == default) continue;
                        newPort.Connect(targetPort);
                    }
                }
            }

            return copy;
        }

        /// <summary>
        /// 清空所有节点
        /// </summary>
        public virtual void Clear()
        {
            for (int i = mNodes.Count - 1; i >= 0; i--)
                RemoveNode(mNodes[i]);
        }

        /// <summary>
        /// 获取指定类型的节点
        /// </summary>
        public T GetNode<T>() where T : Node
        {
            for (int i = 0; i < mNodes.Count; i++)
                if (mNodes[i] is T t) return t;
            return null;
        }

        /// <summary>
        /// 获取所有指定类型的节点
        /// </summary>
        public void GetNodes<T>(List<T> result) where T : Node
        {
            result.Clear();
            for (int i = 0; i < mNodes.Count; i++)
                if (mNodes[i] is T t) result.Add(t);
        }
    }
}
