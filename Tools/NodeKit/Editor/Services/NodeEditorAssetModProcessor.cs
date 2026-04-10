using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    internal sealed class NodeEditorAssetModProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
        {
            if (Path.GetExtension(path) != ".cs")
                return AssetDeleteResult.DidNotDelete;

            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj is not MonoScript script)
                return AssetDeleteResult.DidNotDelete;

            var scriptType = script.GetClass();
            if (scriptType == default || (scriptType != typeof(Node) && !scriptType.IsSubclassOf(typeof(Node))))
                return AssetDeleteResult.DidNotDelete;

            var guids = AssetDatabase.FindAssets("t:" + scriptType.Name);
            for (int i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                for (int j = 0; j < objs.Length; j++)
                {
                    if (objs[j] is not Node node || node.GetType() != scriptType || node.Graph == default)
                        continue;

                    Debug.LogWarning($"{node.name} of {node.Graph} depended on deleted script and has been removed automatically.", node.Graph);
                    node.Graph.RemoveNode(node);
                }
            }

            return AssetDeleteResult.DidNotDelete;
        }

        [InitializeOnLoadMethod]
        private static void OnReloadEditor()
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(NodeGraph).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var graph = AssetDatabase.LoadAssetAtPath<NodeGraph>(assetPath);
                if (graph == default) continue;

                SyncGraphSubAssets(graph, assetPath);
            }
        }

        internal static void SyncGraphSubAssets(NodeGraph graph, string assetPath)
        {
            var serializedObject = new SerializedObject(graph);
            var nodesProp = serializedObject.FindProperty("mNodes");
            if (nodesProp == default) return;

            for (int i = nodesProp.arraySize - 1; i >= 0; i--)
            {
                if (nodesProp.GetArrayElementAtIndex(i).objectReferenceValue == default)
                    nodesProp.DeleteArrayElementAtIndex(i);
            }

            var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is not Node node) continue;
                if (!ContainsNode(nodesProp, node))
                {
                    nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize);
                    nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1).objectReferenceValue = node;
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(graph);
        }

        private static bool ContainsNode(SerializedProperty nodesProp, Node node)
        {
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                if (nodesProp.GetArrayElementAtIndex(i).objectReferenceValue == node)
                    return true;
            }

            return false;
        }
    }
}
