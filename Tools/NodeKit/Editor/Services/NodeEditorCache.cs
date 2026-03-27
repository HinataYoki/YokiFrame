using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 编辑器缓存，缓存 SerializedObject 和编辑器实例
    /// </summary>
    public static class NodeEditorCache
    {
        private static Dictionary<Node, SerializedObject> sSerializedObjects = new();
        private static Dictionary<Node, NodeEditorBase> sNodeEditors = new();
        private static Dictionary<NodeGraph, NodeGraphEditorBase> sGraphEditors = new();

        /// <summary>
        /// 获取节点的 SerializedObject（带缓存）
        /// </summary>
        public static SerializedObject GetSerializedObject(Node node)
        {
            if (node == default) return null;
            if (sSerializedObjects.TryGetValue(node, out var so))
            {
                if (so != default && so.targetObject != default)
                    return so;
                sSerializedObjects.Remove(node);
            }
            so = new SerializedObject(node);
            sSerializedObjects[node] = so;
            return so;
        }

        /// <summary>
        /// 获取节点编辑器（带缓存）
        /// </summary>
        public static NodeEditorBase GetNodeEditor(Node node, NodeGraphView graphView)
        {
            if (node == default) return null;
            if (sNodeEditors.TryGetValue(node, out var editor))
            {
                if (editor != default)
                    return editor;
                sNodeEditors.Remove(node);
            }
            var editorType = NodeReflection.GetNodeEditorType(node.GetType());
            editor = Activator.CreateInstance(editorType) as NodeEditorBase;
            editor.Initialize(node, graphView);
            sNodeEditors[node] = editor;
            return editor;
        }

        /// <summary>
        /// 获取图编辑器（带缓存）
        /// </summary>
        public static NodeGraphEditorBase GetGraphEditor(NodeGraph graph, NodeGraphView graphView)
        {
            if (graph == default) return null;
            if (sGraphEditors.TryGetValue(graph, out var editor))
            {
                if (editor != default)
                    return editor;
                sGraphEditors.Remove(graph);
            }
            var editorType = NodeReflection.GetGraphEditorType(graph.GetType());
            editor = Activator.CreateInstance(editorType) as NodeGraphEditorBase;
            editor.Initialize(graph, graphView);
            sGraphEditors[graph] = editor;
            return editor;
        }

        /// <summary>
        /// 清除节点缓存
        /// </summary>
        public static void ClearNodeCache(Node node)
        {
            if (node == default) return;
            sSerializedObjects.Remove(node);
            sNodeEditors.Remove(node);
        }

        /// <summary>
        /// 清除图缓存
        /// </summary>
        public static void ClearGraphCache(NodeGraph graph)
        {
            if (graph == default) return;
            sGraphEditors.Remove(graph);
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void ClearAll()
        {
            sSerializedObjects.Clear();
            sNodeEditors.Clear();
            sGraphEditors.Clear();
        }

        /// <summary>
        /// 清理无效缓存
        /// </summary>
        public static void Cleanup()
        {
            var nodesToRemove = new List<Node>();
            foreach (var kvp in sSerializedObjects)
            {
                if (kvp.Key == default || kvp.Value == default || kvp.Value.targetObject == default)
                    nodesToRemove.Add(kvp.Key);
            }
            for (int i = 0; i < nodesToRemove.Count; i++)
            {
                var node = nodesToRemove[i];
                sSerializedObjects.Remove(node);
                sNodeEditors.Remove(node);
            }

            var graphsToRemove = new List<NodeGraph>();
            foreach (var kvp in sGraphEditors)
            {
                if (kvp.Key == default)
                    graphsToRemove.Add(kvp.Key);
            }
            for (int i = 0; i < graphsToRemove.Count; i++)
                sGraphEditors.Remove(graphsToRemove[i]);
        }
    }
}
