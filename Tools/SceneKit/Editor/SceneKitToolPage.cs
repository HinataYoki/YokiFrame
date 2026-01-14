#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit 工具页面 - 场景管理器
    /// </summary>
    [YokiToolPage(
        kit: "SceneKit",
        name: "SceneKit",
        icon: KitIcons.SCENEKIT,
        priority: 45,
        category: YokiPageCategory.Tool)]
    public partial class SceneKitToolPage : YokiToolPageBase
    {

        #region 私有字段

        private readonly List<SceneInfo> mScenes = new(16);
        private int mSelectedSceneIndex = -1;

        // UI 元素
        private Label mSceneCountLabel;
        private ListView mSceneListView;
        private VisualElement mDetailPanel;
        private VisualElement mEmptyState;

        // 详情面板元素
        private Label mDetailSceneName;
        private Label mDetailBuildIndex;
        private Label mDetailState;
        private Label mDetailProgress;
        private Label mDetailLoadMode;
        private Label mDetailIsSuspended;
        private Label mDetailIsPreloaded;
        private Label mDetailHasData;

        #endregion

        #region 数据结构

        private struct SceneInfo
        {
            public string SceneName;
            public int BuildIndex;
            public SceneState State;
            public float Progress;
            public SceneLoadMode LoadMode;
            public bool IsSuspended;
            public bool IsPreloaded;
            public bool HasData;
            public bool IsActive;
            public SceneHandler Handler;
        }

        #endregion

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            root.Add(toolbar);

            var refreshBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshScenes);
            toolbar.Add(refreshBtn);

            var unloadAllBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.DELETE, "卸载全部", UnloadAllScenes);
            toolbar.Add(unloadAllBtn);

            var spacer = YokiFrameUIComponents.CreateFlexSpacer();
            toolbar.Add(spacer);

            mSceneCountLabel = new Label("0 个场景");
            mSceneCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mSceneCountLabel);

            // 主内容区域
            var splitView = CreateSplitView(320f);
            root.Add(splitView);

            // 左侧：场景列表
            var leftPanel = CreateLeftPanel();
            splitView.Add(leftPanel);

            // 右侧：详情面板
            var rightPanel = CreateRightPanel();
            splitView.Add(rightPanel);

            // 初始加载
            RefreshScenes();
        }

        [System.Obsolete("保留用于运行时场景刷新")]
        public override void OnUpdate()
        {
            // 编辑器运行时自动刷新
            if (IsPlaying)
            {
                RefreshScenes();
            }
        }

        #region 数据操作

        private void RefreshScenes()
        {
            mScenes.Clear();

            // 获取 SceneKit 管理的场景
            var loadedScenes = SceneKit.GetLoadedScenes();
            var activeScene = SceneManager.GetActiveScene();

            foreach (var handler in loadedScenes)
            {
                if (handler == null) continue;

                mScenes.Add(new SceneInfo
                {
                    SceneName = handler.SceneName ?? "Unknown",
                    BuildIndex = handler.BuildIndex,
                    State = handler.State,
                    Progress = handler.Progress,
                    LoadMode = handler.LoadMode,
                    IsSuspended = handler.IsSuspended,
                    IsPreloaded = handler.IsPreloaded,
                    HasData = handler.SceneData != null,
                    IsActive = handler.Scene.IsValid() && handler.Scene == activeScene,
                    Handler = handler
                });
            }

            // 如果 SceneKit 没有管理任何场景，显示 Unity 当前加载的场景
            if (mScenes.Count == 0)
            {
                int sceneCount = SceneManager.sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid()) continue;

                    mScenes.Add(new SceneInfo
                    {
                        SceneName = scene.name,
                        BuildIndex = scene.buildIndex,
                        State = scene.isLoaded ? SceneState.Loaded : SceneState.None,
                        Progress = scene.isLoaded ? 1f : 0f,
                        LoadMode = SceneLoadMode.Single,
                        IsSuspended = false,
                        IsPreloaded = false,
                        HasData = false,
                        IsActive = scene == activeScene,
                        Handler = null
                    });
                }
            }

            mSceneCountLabel.text = $"{mScenes.Count} 个场景";
            mSceneListView.itemsSource = mScenes;
            mSceneListView.RefreshItems();

            // 更新详情面板
            if (mSelectedSceneIndex >= 0 && mSelectedSceneIndex < mScenes.Count)
            {
                UpdateDetailPanel(mScenes[mSelectedSceneIndex]);
            }
        }

        private void OnSceneSelectionChanged(IEnumerable<object> selection)
        {
            mSelectedSceneIndex = mSceneListView.selectedIndex;

            if (mSelectedSceneIndex < 0 || mSelectedSceneIndex >= mScenes.Count)
            {
                mDetailPanel.style.display = DisplayStyle.None;
                mEmptyState.style.display = DisplayStyle.Flex;
                return;
            }

            var scene = mScenes[mSelectedSceneIndex];
            mDetailPanel.style.display = DisplayStyle.Flex;
            mEmptyState.style.display = DisplayStyle.None;

            UpdateDetailPanel(scene);
        }

        private void UpdateDetailPanel(SceneInfo scene)
        {
            mDetailSceneName.text = scene.SceneName;
            mDetailBuildIndex.text = $"Build Index: {scene.BuildIndex}";
            mDetailState.text = GetStateText(scene.State);
            mDetailProgress.text = $"{scene.Progress:P0}";
            mDetailLoadMode.text = scene.LoadMode == SceneLoadMode.Single ? "Single" : "Additive";
            mDetailIsSuspended.text = scene.IsSuspended ? "是" : "否";
            mDetailIsPreloaded.text = scene.IsPreloaded ? "是" : "否";
            mDetailHasData.text = scene.HasData ? "有数据" : "无数据";
        }

        private void UnloadSelectedScene()
        {
            if (mSelectedSceneIndex < 0 || mSelectedSceneIndex >= mScenes.Count) return;

            var scene = mScenes[mSelectedSceneIndex];

            if (scene.IsActive && SceneManager.sceneCount <= 1)
            {
                EditorUtility.DisplayDialog("无法卸载", "无法卸载唯一的活动场景", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认卸载",
                $"确定要卸载场景 \"{scene.SceneName}\" 吗？",
                "卸载", "取消"))
            {
                return;
            }

            if (scene.Handler != null)
            {
                SceneKit.UnloadSceneAsync(scene.Handler, RefreshScenes);
            }
            else
            {
                SceneManager.UnloadSceneAsync(scene.SceneName);
                EditorApplication.delayCall += RefreshScenes;
            }
        }

        private void SetSelectedSceneActive()
        {
            if (mSelectedSceneIndex < 0 || mSelectedSceneIndex >= mScenes.Count) return;

            var scene = mScenes[mSelectedSceneIndex];

            if (scene.State != SceneState.Loaded)
            {
                EditorUtility.DisplayDialog("无法设置", "只能将已加载的场景设为活动场景", "确定");
                return;
            }

            if (scene.Handler != null && scene.Handler.Scene.IsValid())
            {
                SceneManager.SetActiveScene(scene.Handler.Scene);
            }
            else
            {
                var unityScene = SceneManager.GetSceneByName(scene.SceneName);
                if (unityScene.IsValid())
                {
                    SceneManager.SetActiveScene(unityScene);
                }
            }

            RefreshScenes();
        }

        private void UnloadAllScenes()
        {
            if (mScenes.Count <= 1)
            {
                EditorUtility.DisplayDialog("无法卸载", "至少需要保留一个场景", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认卸载全部",
                "确定要卸载所有非活动场景吗？",
                "卸载", "取消"))
            {
                return;
            }

            SceneKit.ClearAllScenes(true, RefreshScenes);
        }

        #endregion
    }
}
#endif
