using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YokiFrame.NodeKit
{
    public abstract class NodeGraph : ScriptableObject
    {
        [SerializeField] protected List<Node> mNodes = new();
        [SerializeField] private Node mStartNode;
        [NonSerialized] private bool mEnsuringRequiredNodes;

        public IReadOnlyList<Node> Nodes => mNodes;
        public Node StartNode => GetStartNode();
        public bool HasExplicitStartNode => mStartNode != default && mStartNode.Graph == this;
        public Node ExplicitStartNode => HasExplicitStartNode ? mStartNode : null;

        public T AddNode<T>() where T : Node => AddNode(typeof(T)) as T;

        public virtual Node AddNode(Type type)
        {
            if (!typeof(Node).IsAssignableFrom(type))
            {
                Debug.LogError($"[NodeKit] Type {type} is not a Node");
                return null;
            }

            Node.GraphHotfix = this;
            var node = CreateInstance(type) as Node;
            if (node == default) return null;

            node.Graph = this;
            node.name = type.Name.EndsWith("Node") ? type.Name[..^4] : type.Name;
            node.UpdatePorts();
            mNodes.Add(node);
            if (mStartNode == default)
                mStartNode = node;

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
#endif
            EnsureRequiredNodes();
            return node;
        }

        public virtual void RemoveNode(Node node)
        {
            if (node == default) return;
            if (!CanRemoveNode(node)) return;
            node.ClearAllConnections();
            if (mStartNode == node)
                mStartNode = null;
            mNodes.Remove(node);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
#endif
            DestroyImmediate(node, true);
        }

        public virtual Node CopyNode(Node original)
        {
            if (original == default) return null;
            var copy = Instantiate(original);
            copy.Graph = this;
            copy.name = original.name;
            copy.ClearAllConnections();
            copy.UpdatePorts();
            mNodes.Add(copy);
            if (mStartNode == original)
                mStartNode = copy;

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(copy, this);
#endif
            return copy;
        }

        public virtual bool MoveNodeToFront(Node node)
        {
            if (node == default) return false;

            int index = mNodes.IndexOf(node);
            if (index < 0 || index == mNodes.Count - 1)
                return false;

            mNodes.RemoveAt(index);
            mNodes.Add(node);
            return true;
        }

        public virtual void SetStartNode(Node node)
        {
            if (node == default || node.Graph != this) return;
            mStartNode = node;
        }

        public virtual void ClearStartNode()
        {
            mStartNode = null;
        }

        public virtual Node GetStartNode()
        {
            if (HasExplicitStartNode)
                return mStartNode;

            for (int i = 0; i < mNodes.Count; i++)
            {
                var node = mNodes[i];
                if (node != default && node.GetType().Name == "StartNode")
                    return node;
            }

            return mNodes.Count > 0 ? mNodes[0] : null;
        }

        public virtual NodeGraph Copy()
        {
            var copy = Instantiate(this);
            copy.name = name + " (Copy)";

            var nodeMap = new Dictionary<Node, Node>();
            for (int i = 0; i < mNodes.Count; i++)
            {
                var original = mNodes[i];
                var newNode = copy.mNodes[i];
                nodeMap[original] = newNode;
                newNode.Graph = copy;
            }

            if (mStartNode != default && nodeMap.TryGetValue(mStartNode, out var copiedStartNode))
                copy.mStartNode = copiedStartNode;

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
                        var reroutePoints = port.GetReroutePoints(j);
                        var newConnectionIndex = newPort.GetConnectionIndex(targetPort);
                        var newReroutePoints = newPort.GetReroutePoints(newConnectionIndex);
                        if (reroutePoints != default && newReroutePoints != default)
                            newReroutePoints.AddRange(reroutePoints);
                    }
                }
            }

            return copy;
        }

        public virtual void Clear()
        {
            for (int i = mNodes.Count - 1; i >= 0; i--)
                RemoveNode(mNodes[i]);
        }

        public virtual bool CanRemoveNode(Node node)
        {
            if (node == default) return false;

            var attribs = GetType().GetCustomAttributes(typeof(RequireNodeAttribute), true);
            for (int i = 0; i < attribs.Length; i++)
            {
                if (attribs[i] is not RequireNodeAttribute require || !require.Requires(node.GetType()))
                    continue;

                int count = mNodes.Count(x => x != default && x.GetType() == node.GetType());
                if (count <= 1)
                    return false;
            }

            return true;
        }

        public T GetNode<T>() where T : Node
        {
            for (int i = 0; i < mNodes.Count; i++)
                if (mNodes[i] is T t) return t;
            return null;
        }

        public void GetNodes<T>(List<T> result) where T : Node
        {
            result.Clear();
            for (int i = 0; i < mNodes.Count; i++)
                if (mNodes[i] is T t) result.Add(t);
        }

        protected virtual void OnEnable()
        {
            EnsureRequiredNodes();
        }

        private void EnsureRequiredNodes()
        {
            if (mEnsuringRequiredNodes) return;
            mEnsuringRequiredNodes = true;
            var attribs = GetType().GetCustomAttributes(typeof(RequireNodeAttribute), true);
            try
            {
                for (int i = 0; i < attribs.Length; i++)
                {
                    if (attribs[i] is not RequireNodeAttribute require)
                        continue;

                    EnsureRequiredNode(require.Type0);
                    EnsureRequiredNode(require.Type1);
                    EnsureRequiredNode(require.Type2);
                }
            }
            finally
            {
                mEnsuringRequiredNodes = false;
            }
        }

        private void EnsureRequiredNode(Type requiredType)
        {
            if (requiredType == default || !typeof(Node).IsAssignableFrom(requiredType))
                return;

            for (int i = 0; i < mNodes.Count; i++)
            {
                if (mNodes[i] != default && mNodes[i].GetType() == requiredType)
                    return;
            }

            AddNode(requiredType);
        }
    }
}
