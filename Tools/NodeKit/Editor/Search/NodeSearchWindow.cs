using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 节点搜索窗口
    /// </summary>
    public class NodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private NodeGraphView mGraphView;
        private Vector2 mMousePosition;
        private Type mCompatibleType;
        private PortIO? mRequiredDirection;
        private NodePort mSourcePort;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(NodeGraphView graphView)
        {
            mGraphView = graphView;
        }

        /// <summary>
        /// 设置鼠标位置
        /// </summary>
        public void SetMousePosition(Vector2 position)
        {
            mMousePosition = position;
        }

        public void SetCompatibility(NodePort sourcePort)
        {
            mSourcePort = sourcePort;
            if (sourcePort == default)
            {
                mCompatibleType = null;
                mRequiredDirection = null;
                return;
            }

            mCompatibleType = sourcePort.ValueType;
            mRequiredDirection = sourcePort.IsOutput ? PortIO.Input : PortIO.Output;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"))
            };

            var nodeTypes = NodeReflection.GetNodeTypes();
            var groups = new HashSet<string>();
            var graphEditor = mGraphView.GraphEditor;

            // 按菜单路径排序
            var sorted = new List<NodeReflection.NodeTypeInfo>(nodeTypes);
            sorted.Sort((a, b) =>
            {
                string menuA = graphEditor?.GetNodeMenuName(a.Type) ?? a.MenuPath;
                string menuB = graphEditor?.GetNodeMenuName(b.Type) ?? b.MenuPath;
                int orderA = graphEditor?.GetNodeMenuOrder(a.Type) ?? a.Order;
                int orderB = graphEditor?.GetNodeMenuOrder(b.Type) ?? b.Order;
                var cmp = orderA.CompareTo(orderB);
                return cmp != 0 ? cmp : string.Compare(menuA, menuB, StringComparison.Ordinal);
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                var info = sorted[i];
                var path = graphEditor?.GetNodeMenuName(info.Type) ?? info.MenuPath;
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                if (!mGraphView.CanCreateNodeType(info.Type, info.MaxCount))
                    continue;

                if (!string.IsNullOrWhiteSpace(mCompatibleType?.Name) && NodePreferences.CreateFilter && mRequiredDirection.HasValue)
                {
                    if (!NodeEditorUtility.HasCompatiblePortType(info.Type, mCompatibleType, mRequiredDirection.Value))
                        continue;
                }

                var parts = path.Split('/');

                // 添加分组
                var groupPath = "";
                for (int j = 0; j < parts.Length - 1; j++)
                {
                    groupPath += (j > 0 ? "/" : "") + parts[j];
                    if (groups.Add(groupPath))
                    {
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(parts[j]), j + 1));
                    }
                }

                // 添加节点条目
                var entry = new SearchTreeEntry(new GUIContent(parts[^1]))
                {
                    level = parts.Length,
                    userData = info.Type
                };
                tree.Add(entry);
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is not Type type) return false;

            var node = mGraphView.CreateNode(type, mMousePosition);
            if (node != default && mSourcePort != default)
                mGraphView.AutoConnect(node, mSourcePort);

            SetCompatibility(null);
            return true;
        }
    }
}
