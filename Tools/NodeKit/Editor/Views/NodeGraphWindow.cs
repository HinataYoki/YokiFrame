using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 节点图编辑器窗口
    /// </summary>
    public class NodeGraphWindow : EditorWindow
    {
        private NodeGraphView mGraphView;
        private NodeSearchWindow mSearchWindow;
        private NodeGraph mCurrentGraph;

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId);
            if (asset is not NodeGraph graph) return false;

            var window = GetWindow<NodeGraphWindow>();
            window.LoadGraph(graph);
            return true;
        }

        [MenuItem("YokiFrame/NodeKit/Node Graph Editor")]
        public static void ShowWindow()
        {
            GetWindow<NodeGraphWindow>("Node Graph");
        }

        private void OnEnable()
        {
            CreateGraphView();
            CreateToolbar();
            CreateSearchWindow();

            // 重新加载之前的图
            if (mCurrentGraph != default)
                mGraphView.LoadGraph(mCurrentGraph);
        }

        private void OnDisable()
        {
            if (mGraphView != default)
                rootVisualElement.Remove(mGraphView);
        }

        private void OnFocus()
        {
            // 刷新图
            if (mCurrentGraph != default && mGraphView != default)
            {
                // 验证资产是否仍然有效
                if (mCurrentGraph == default)
                {
                    mGraphView.ClearGraph();
                    mCurrentGraph = null;
                }
            }
        }

        private void CreateGraphView()
        {
            mGraphView = new NodeGraphView
            {
                name = "NodeGraphView"
            };
            mGraphView.StretchToParentSize();
            rootVisualElement.Add(mGraphView);

            // 注册键盘事件
            mGraphView.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("yoki-toolbar");

            var saveButton = new ToolbarButton(() => mGraphView.SaveGraph())
            {
                text = "Save"
            };
            toolbar.Add(saveButton);

            var frameAllButton = new ToolbarButton(() => mGraphView.FrameAll())
            {
                text = "Frame All"
            };
            toolbar.Add(frameAllButton);

            rootVisualElement.Add(toolbar);
        }

        private void CreateSearchWindow()
        {
            mSearchWindow = CreateInstance<NodeSearchWindow>();
            mSearchWindow.Initialize(mGraphView);

            mGraphView.nodeCreationRequest = ctx =>
            {
                mSearchWindow.SetMousePosition(ctx.screenMousePosition - position.position);
                SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), mSearchWindow);
            };
        }

        /// <summary>
        /// 加载节点图
        /// </summary>
        public void LoadGraph(NodeGraph graph)
        {
            mCurrentGraph = graph;
            titleContent = new GUIContent(graph.name);
            mGraphView.LoadGraph(graph);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Delete 键删除选中元素
            if (evt.keyCode == KeyCode.Delete)
            {
                mGraphView.DeleteSelection();
                evt.StopPropagation();
            }
            // Ctrl+D 复制
            else if (evt.keyCode == KeyCode.D && evt.ctrlKey)
            {
                DuplicateSelection();
                evt.StopPropagation();
            }
        }

        private void DuplicateSelection()
        {
            var selection = mGraphView.selection;
            for (int i = 0; i < selection.Count; i++)
            {
                if (selection[i] is NodeView nodeView)
                    mGraphView.DuplicateNode(nodeView);
            }
        }
    }
}
