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
    /// SceneKit 工具页。
    /// 用于查看当前已加载场景、场景状态以及常用场景操作。
    /// </summary>
    [YokiToolPage(
        kit: "SceneKit",
        name: "SceneKit",
        icon: KitIcons.SCENEKIT,
        priority: 45,
        category: YokiPageCategory.Tool)]
    public partial class SceneKitToolPage : YokiToolPageBase
    {
        private readonly List<SceneInfo> mScenes = new(16);
        private int mSelectedSceneIndex = -1;

        private Label mSceneCountLabel;
        private Label mLoadedSceneMetricLabel;
        private Label mActiveSceneMetricLabel;
        private Label mSelectedSceneMetricLabel;
        private ListView mSceneListView;
        private VisualElement mDetailPanel;
        private VisualElement mEmptyState;

        private Label mDetailSceneName;
        private Label mDetailBuildIndex;
        private Label mDetailState;
        private Label mDetailProgress;
        private Label mDetailLoadMode;
        private Label mDetailIsSuspended;
        private Label mDetailIsPreloaded;
        private Label mDetailHasData;

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

        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "SceneKit",
                "统一查看 SceneKit 已管理场景与 Unity 当前已加载场景，并保留常用卸载与激活操作。",
                KitIcons.SCENEKIT,
                "场景工作台");
            root.Add(scaffold.Root);
            scaffold.Toolbar.style.display = DisplayStyle.None;

            scaffold.Content.Add(CreateToolbarSection());
            SetStatusContent(scaffold.StatusBar, CreateKitStatusBanner(
                "数据来源",
                "如果 SceneKit 当前没有接管场景，本页会自动回退为显示 Unity 当前已加载的场景。"));

            var metricStrip = CreateKitMetricStrip();
            scaffold.Content.Add(metricStrip);

            var (loadedCard, loadedValue) = CreateKitMetricCard("场景总数", "0", "当前列表中的场景数量", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mLoadedSceneMetricLabel = loadedValue;
            metricStrip.Add(loadedCard);

            var (activeCard, activeValue) = CreateKitMetricCard("活动场景", "-", "当前 Unity 活动场景名称", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mActiveSceneMetricLabel = activeValue;
            metricStrip.Add(activeCard);

            var (selectedCard, selectedValue) = CreateKitMetricCard("当前选中", "-", "未选择场景时显示为空", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mSelectedSceneMetricLabel = selectedValue;
            metricStrip.Add(selectedCard);

            var splitView = CreateSplitView(320f);
            scaffold.Content.Add(splitView);
            splitView.Add(CreateLeftPanel());
            splitView.Add(CreateRightPanel());

            RefreshScenes();
        }

        private VisualElement CreateToolbarSection()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            toolbar.AddToClassList("yoki-kit-inline-toolbar");
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshScenes));
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.DELETE, "卸载全部", UnloadAllScenes));
            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            mSceneCountLabel = new Label("0 个场景");
            mSceneCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mSceneCountLabel);
            return toolbar;
        }

        [System.Obsolete("保留用于运行时场景刷新。")]
        public override void OnUpdate()
        {
            if (IsPlaying)
            {
                RefreshScenes();
            }
        }

        private void RefreshScenes()
        {
            string previousSelectedSceneName = mSelectedSceneIndex >= 0 && mSelectedSceneIndex < mScenes.Count
                ? mScenes[mSelectedSceneIndex].SceneName
                : null;

            mScenes.Clear();
            Scene activeScene = SceneManager.GetActiveScene();

            foreach (SceneHandler handler in SceneKit.GetLoadedScenes())
            {
                if (handler == null)
                {
                    continue;
                }

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

            if (mScenes.Count == 0)
            {
                int sceneCount = SceneManager.sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid())
                    {
                        continue;
                    }

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
            mLoadedSceneMetricLabel.text = mScenes.Count.ToString();
            mActiveSceneMetricLabel.text = activeScene.IsValid() ? activeScene.name : "-";

            mSceneListView.itemsSource = mScenes;
            mSceneListView.RefreshItems();
            RestoreSceneSelection(previousSelectedSceneName);
        }

        private void RestoreSceneSelection(string previousSelectedSceneName)
        {
            if (!string.IsNullOrEmpty(previousSelectedSceneName))
            {
                for (int i = 0; i < mScenes.Count; i++)
                {
                    if (mScenes[i].SceneName != previousSelectedSceneName)
                    {
                        continue;
                    }

                    mSelectedSceneIndex = i;
                    mSceneListView.SetSelectionWithoutNotify(new[] { i });
                    ApplySceneSelectionState(i);
                    return;
                }
            }

            mSelectedSceneIndex = -1;
            mSceneListView.ClearSelection();
            ApplySceneSelectionState(-1);
        }

        private void OnSceneSelectionChanged(IEnumerable<object> selection)
        {
            mSelectedSceneIndex = mSceneListView.selectedIndex;
            ApplySceneSelectionState(mSelectedSceneIndex);
        }

        private void ApplySceneSelectionState(int sceneIndex)
        {
            if (sceneIndex < 0 || sceneIndex >= mScenes.Count)
            {
                mDetailPanel.style.display = DisplayStyle.None;
                mEmptyState.style.display = DisplayStyle.Flex;
                mSelectedSceneMetricLabel.text = "-";
                return;
            }

            SceneInfo scene = mScenes[sceneIndex];
            mDetailPanel.style.display = DisplayStyle.Flex;
            mEmptyState.style.display = DisplayStyle.None;
            mSelectedSceneMetricLabel.text = scene.SceneName;
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
            if (mSelectedSceneIndex < 0 || mSelectedSceneIndex >= mScenes.Count)
            {
                return;
            }

            SceneInfo scene = mScenes[mSelectedSceneIndex];
            if (scene.IsActive && SceneManager.sceneCount <= 1)
            {
                EditorUtility.DisplayDialog("无法卸载", "无法卸载唯一的活动场景。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认卸载", $"确定要卸载场景 “{scene.SceneName}” 吗？", "卸载", "取消"))
            {
                return;
            }

            if (scene.Handler != null)
            {
                SceneKit.UnloadSceneAsync(scene.Handler, RefreshScenes);
                return;
            }

            SceneManager.UnloadSceneAsync(scene.SceneName);
            EditorApplication.delayCall += RefreshScenes;
        }

        private void SetSelectedSceneActive()
        {
            if (mSelectedSceneIndex < 0 || mSelectedSceneIndex >= mScenes.Count)
            {
                return;
            }

            SceneInfo scene = mScenes[mSelectedSceneIndex];
            if (scene.State != SceneState.Loaded)
            {
                EditorUtility.DisplayDialog("无法设置", "只能将已加载的场景设为活动场景。", "确定");
                return;
            }

            if (scene.Handler != null && scene.Handler.Scene.IsValid())
            {
                SceneManager.SetActiveScene(scene.Handler.Scene);
            }
            else
            {
                Scene unityScene = SceneManager.GetSceneByName(scene.SceneName);
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
                EditorUtility.DisplayDialog("无法卸载", "至少需要保留一个场景。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认卸载全部", "确定要卸载所有非活动场景吗？", "卸载", "取消"))
            {
                return;
            }

            SceneKit.ClearAllScenes(true, RefreshScenes);
        }
    }
}
#endif
