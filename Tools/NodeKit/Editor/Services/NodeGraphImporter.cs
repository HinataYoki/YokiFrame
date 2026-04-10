using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    internal sealed class NodeGraphImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int i = 0; i < importedAssets.Length; i++)
            {
                var path = importedAssets[i];
                if (Path.GetExtension(path) != ".asset")
                    continue;

                var graph = AssetDatabase.LoadAssetAtPath<NodeGraph>(path);
                if (graph == default)
                    continue;

                NodeEditorAssetModProcessor.SyncGraphSubAssets(graph, path);
                EnsureRequiredNodes(graph);
            }
        }

        private static void EnsureRequiredNodes(NodeGraph graph)
        {
            var graphType = graph.GetType();
            var attribs = graphType.GetCustomAttributes(typeof(RequireNodeAttribute), true);
            var position = Vector2.zero;

            for (int i = 0; i < attribs.Length; i++)
            {
                if (attribs[i] is not RequireNodeAttribute require)
                    continue;

                AddRequired(graph, require.Type0, ref position);
                AddRequired(graph, require.Type1, ref position);
                AddRequired(graph, require.Type2, ref position);
            }
        }

        private static void AddRequired(NodeGraph graph, Type type, ref Vector2 position)
        {
            if (type == default || !typeof(Node).IsAssignableFrom(type))
                return;

            var nodes = graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != default && nodes[i].GetType() == type)
                    return;
            }

            var node = graph.AddNode(type);
            if (node == default) return;

            node.Position = position;
            position.x += 240f;
            if (string.IsNullOrWhiteSpace(node.name))
                node.name = NodeEditorUtility.NodeDefaultName(type);

            var graphPath = AssetDatabase.GetAssetPath(graph);
            if (!string.IsNullOrEmpty(graphPath))
                AssetDatabase.AddObjectToAsset(node, graph);
        }
    }
}
