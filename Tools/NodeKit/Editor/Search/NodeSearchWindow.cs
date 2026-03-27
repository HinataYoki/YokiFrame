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

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"))
            };

            var nodeTypes = NodeReflection.GetNodeTypes();
            var groups = new HashSet<string>();

            // 按菜单路径排序
            var sorted = new List<NodeReflection.NodeTypeInfo>(nodeTypes);
            sorted.Sort((a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.MenuPath, b.MenuPath, StringComparison.Ordinal);
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                var info = sorted[i];
                var path = info.MenuPath;
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

            var worldPos = mMousePosition - 
                new Vector2(mGraphView.viewTransform.position.x, mGraphView.viewTransform.position.y);
            worldPos /= mGraphView.viewTransform.scale.x;

            mGraphView.CreateNode(type, worldPos);
            return true;
        }
    }
}
