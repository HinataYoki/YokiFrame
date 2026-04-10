using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    [CustomEditor(typeof(NodeGraph), true)]
    [CanEditMultipleObjects]
    public class GlobalGraphEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (GUILayout.Button("Edit graph", GUILayout.Height(40)))
                NodeGraphWindow.ShowGraph(serializedObject.targetObject as NodeGraph);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Raw data", EditorStyles.boldLabel);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(Node), true)]
    [CanEditMultipleObjects]
    public class GlobalNodeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (GUILayout.Button("Edit graph", GUILayout.Height(40)))
            {
                var graphProperty = serializedObject.FindProperty("mGraph");
                if (graphProperty?.objectReferenceValue is NodeGraph graph)
                    NodeGraphWindow.ShowGraph(graph, serializedObject.targetObject as Node);
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Raw data", EditorStyles.boldLabel);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
