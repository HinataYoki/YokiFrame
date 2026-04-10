using UnityEditor;

namespace YokiFrame.NodeKit.Editor
{
    internal sealed class GraphRenameFixAssetProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int i = 0; i < movedAssets.Length; i++)
            {
                var nodeAsset = AssetDatabase.LoadMainAssetAtPath(movedAssets[i]) as Node;
                if (nodeAsset == default || !AssetDatabase.IsMainAsset(nodeAsset) || nodeAsset.Graph == default)
                    continue;

                AssetDatabase.SetMainObject(nodeAsset.Graph, movedAssets[i]);
                AssetDatabase.ImportAsset(movedAssets[i]);

                nodeAsset.name = NodeEditorUtility.NodeDefaultName(nodeAsset.GetType());
                EditorUtility.SetDirty(nodeAsset);
            }
        }
    }
}
