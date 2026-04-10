using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

namespace YokiFrame.NodeKit.Editor
{
    [InitializeOnLoad]
    public class NodeGraphWindow : EditorWindow
    {
        private static int sLastSelectionInstanceId;
        private NodeGraphView mGraphView;
        private NodeSearchWindow mSearchWindow;
        private NodeGraph mCurrentGraph;
        private IMGUIContainer mOverlayGui;

        static NodeGraphWindow()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

[OnOpenAsset]
#if UNITY_6000_0_OR_NEWER
public static bool OnOpenAsset(UnityEngine.EntityId entityId, int line)
{
    var asset = EditorUtility.EntityIdToObject(entityId);
    if (asset is not NodeGraph graph) return false;

    ShowGraph(graph);
    return true;
}
#else
public static bool OnOpenAsset(int instanceId, int line)
{
    var asset = EditorUtility.InstanceIDToObject(instanceId);
    if (asset is not NodeGraph graph) return false;

    ShowGraph(graph);
    return true;
}
#endif

        [MenuItem("YokiFrame/NodeKit/Node Graph Editor")]
        public static void ShowWindow()
        {
            GetWindow<NodeGraphWindow>("Node Graph");
        }

        public static NodeGraphWindow ShowGraph(NodeGraph graph)
        {
            var window = GetWindow<NodeGraphWindow>("Node Graph");
            if (graph != default)
                window.LoadGraph(graph);
            return window;
        }

        public static NodeGraphWindow ShowGraph(NodeGraph graph, Node focusNode)
        {
            var window = ShowGraph(graph);
            if (focusNode != default)
                window.FocusNode(focusNode);
            return window;
        }

        public static void RepaintAll()
        {
            var windows = Resources.FindObjectsOfTypeAll<NodeGraphWindow>();
            for (int i = 0; i < windows.Length; i++)
                windows[i].Repaint();
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();
            CreateGraphView();
            CreateToolbar();
            CreateSearchWindow();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.projectChanged += OnProjectChanged;

            if (mCurrentGraph != default)
                mGraphView.LoadGraph(mCurrentGraph);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.projectChanged -= OnProjectChanged;
            if (mGraphView != default)
                rootVisualElement.Remove(mGraphView);
            if (mOverlayGui != default)
                rootVisualElement.Remove(mOverlayGui);
        }

        private void OnFocus()
        {
            if (mCurrentGraph == default || mGraphView == default) return;
            if (mGraphView.GraphEditor != default)
                mGraphView.GraphEditor.OnWindowFocus();
        }

        private void OnLostFocus()
        {
            if (mGraphView?.GraphEditor != default)
                mGraphView.GraphEditor.OnWindowFocusLost();
        }

        private void CreateGraphView()
        {
            mGraphView = new NodeGraphView
            {
                name = "NodeGraphView"
            };
            mGraphView.StretchToParentSize();
            mGraphView.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            mGraphView.RegisterCallback<DragPerformEvent>(OnDragPerform);
            rootVisualElement.Add(mGraphView);
            mGraphView.RegisterCallback<KeyDownEvent>(OnKeyDown);

            mOverlayGui = new IMGUIContainer(DrawOverlayGUI)
            {
                pickingMode = PickingMode.Ignore
            };
            mOverlayGui.StretchToParentSize();
            rootVisualElement.Add(mOverlayGui);
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("yoki-toolbar");

            toolbar.Add(new ToolbarButton(() => mGraphView.SaveGraph()) { text = "Save" });
            toolbar.Add(new ToolbarButton(SaveGraphAs) { text = "Save As" });
            toolbar.Add(new ToolbarButton(() => mGraphView.FrameAll()) { text = "Frame All" });
            toolbar.Add(new ToolbarButton(() => mGraphView.Home()) { text = "Home" });
            toolbar.Add(new ToolbarButton(() => SettingsService.OpenUserPreferences("Preferences/NodeKit")) { text = "Preferences" });

            rootVisualElement.Add(toolbar);
        }

        private void CreateSearchWindow()
        {
            mSearchWindow = CreateInstance<NodeSearchWindow>();
            mSearchWindow.Initialize(mGraphView);
            mGraphView.SetSearchWindow(mSearchWindow);

            mGraphView.nodeCreationRequest = ctx =>
            {
                var windowLocal = ctx.screenMousePosition - position.position;
                var graphLocal = mGraphView.contentViewContainer.WorldToLocal(windowLocal);
                mSearchWindow.SetMousePosition(graphLocal);
                mSearchWindow.SetCompatibility(null);
                SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), mSearchWindow);
            };
        }

        public void LoadGraph(NodeGraph graph)
        {
            bool changedGraph = mCurrentGraph != graph;
            mCurrentGraph = graph;
            titleContent = new GUIContent(graph.name);
            mGraphView.LoadGraph(graph);
            if (changedGraph && mGraphView.GraphEditor != default)
                mGraphView.GraphEditor.OnOpen();
        }

        public void FocusNode(Node node)
        {
            if (node == default || mGraphView == default) return;
            if (node.Graph != default && mCurrentGraph != node.Graph)
                LoadGraph(node.Graph);
            mGraphView.Home(node);
        }

        private void OnUndoRedoPerformed()
        {
            if (mCurrentGraph == default || mGraphView == default) return;
            mGraphView.LoadGraph(mCurrentGraph);
            titleContent = new GUIContent(mCurrentGraph.name);
        }

        private void OnProjectChanged()
        {
            if (mGraphView == default) return;

            if (mCurrentGraph == default)
            {
                titleContent = new GUIContent("Node Graph");
                mGraphView.ClearGraph();
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(mCurrentGraph);
            if (string.IsNullOrEmpty(assetPath))
            {
                titleContent = new GUIContent("Node Graph");
                mGraphView.ClearGraph();
                mCurrentGraph = null;
                return;
            }

            titleContent = new GUIContent(mCurrentGraph.name);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                mGraphView.DeleteSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.D && evt.ctrlKey)
            {
                DuplicateSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.F2)
            {
                RenameSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.C && evt.ctrlKey)
            {
                mGraphView.CopySelectionNodes();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.V && evt.ctrlKey)
            {
                mGraphView.PasteNodes(mGraphView.GetPreferredPastePosition());
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.F)
            {
                mGraphView.Home();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.A && evt.ctrlKey)
            {
                ToggleSelectAll();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.S && evt.ctrlKey)
            {
                mGraphView.SaveGraph();
                evt.StopPropagation();
            }
        }

        private void DrawOverlayGUI()
        {
            if (mGraphView?.GraphEditor == default)
                return;

            mGraphView.GraphEditor.OnGUI();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (mGraphView?.GraphEditor == default)
                return;

            if (DragAndDrop.objectReferences != default && DragAndDrop.objectReferences.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (mGraphView?.GraphEditor == default)
                return;

            var droppedObjects = DragAndDrop.objectReferences;
            if (droppedObjects == default || droppedObjects.Length == 0)
                return;

            DragAndDrop.AcceptDrag();
            mGraphView.GraphEditor.OnDropObjects(droppedObjects);
            evt.StopPropagation();
        }

        private void DuplicateSelection()
        {
            mGraphView.CopySelectionNodes();
            mGraphView.DuplicateSelectionNodes();
        }

        private void RenameSelection()
        {
            if (mGraphView.selection.Count != 1) return;
            if (mGraphView.selection[0] is not NodeView nodeView) return;
            RenamePopup.Show(nodeView.Target, _ =>
            {
                nodeView.NodeEditor.OnRename();
                titleContent = new GUIContent(mCurrentGraph != default ? mCurrentGraph.name : "Node Graph");
                mGraphView.LoadGraph(mCurrentGraph);
                mGraphView.SaveGraph();
            });
        }

        private void ToggleSelectAll()
        {
            if (mCurrentGraph == default || mGraphView == default) return;

            bool hasUnselected = false;
            for (int i = 0; i < mCurrentGraph.Nodes.Count; i++)
            {
                var nodeView = mGraphView.GetNodeView(mCurrentGraph.Nodes[i]);
                if (nodeView != default && !mGraphView.selection.Contains(nodeView))
                {
                    hasUnselected = true;
                    break;
                }
            }

            mGraphView.ClearSelection();
            if (!hasUnselected) return;

            for (int i = 0; i < mCurrentGraph.Nodes.Count; i++)
            {
                var nodeView = mGraphView.GetNodeView(mCurrentGraph.Nodes[i]);
                if (nodeView != default)
                    mGraphView.AddToSelection(nodeView);
            }
        }

        private void SaveGraphAs()
        {
            if (mCurrentGraph == default)
                return;

            string currentPath = AssetDatabase.GetAssetPath(mCurrentGraph);
            string suggestedName = string.IsNullOrWhiteSpace(mCurrentGraph.name) ? "NewNodeGraph" : mCurrentGraph.name;
            string path = EditorUtility.SaveFilePanelInProject("Save Node Graph As", suggestedName, "asset", string.Empty);
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!string.IsNullOrWhiteSpace(currentPath) && string.Equals(currentPath, path, System.StringComparison.OrdinalIgnoreCase))
            {
                mGraphView.SaveGraph();
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<NodeGraph>(path) != default)
                AssetDatabase.DeleteAsset(path);

            NodeGraph savedGraph;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                mGraphView.SaveGraph();
                if (!AssetDatabase.CopyAsset(currentPath, path))
                {
                    Debug.LogError($"[NodeKit] Failed to copy graph asset from '{currentPath}' to '{path}'.");
                    return;
                }

                savedGraph = AssetDatabase.LoadAssetAtPath<NodeGraph>(path);
            }
            else
            {
                savedGraph = Instantiate(mCurrentGraph);
                savedGraph.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(savedGraph, path);

                foreach (var node in savedGraph.Nodes)
                {
                    if (node != default)
                        AssetDatabase.AddObjectToAsset(node, savedGraph);
                }
            }

            if (savedGraph == default)
                return;

            AssetDatabase.SaveAssets();
            LoadGraph(savedGraph);
        }

        private static void OnSelectionChanged()
        {
            if (!NodePreferences.OpenOnCreate) return;

            var activeObject = Selection.activeObject;
            if (activeObject is not NodeGraph graph) return;

    #if UNITY_6000_0_OR_NEWER
    int currentInstanceId = activeObject.GetEntityId().GetHashCode();
    #else
    int currentInstanceId = activeObject.GetInstanceID();
    #endif
    if (currentInstanceId == sLastSelectionInstanceId) return;
    sLastSelectionInstanceId = currentInstanceId;

            EditorApplication.delayCall += () =>
            {
                if (Selection.activeObject == graph)
                    ShowGraph(graph);
            };
        }
    }
}
