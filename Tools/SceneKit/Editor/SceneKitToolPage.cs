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
    public class SceneKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "SceneKit";
        public override string PageIcon => KitIcons.SCENEKIT;
        public override int Priority => 45;

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
            var toolbar = CreateToolbar();
            root.Add(toolbar);

            var refreshBtn = CreateToolbarButton("🔄 刷新", RefreshScenes);
            toolbar.Add(refreshBtn);

            var unloadAllBtn = CreateToolbarButton("🗑️ 卸载全部", UnloadAllScenes);
            toolbar.Add(unloadAllBtn);

            var spacer = CreateToolbarSpacer();
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

        public override void OnUpdate()
        {
            // 编辑器运行时自动刷新
            if (IsPlaying)
            {
                RefreshScenes();
            }
        }

        #region UI 构建

        private VisualElement CreateLeftPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("left-panel");

            // 头部
            var header = CreatePanelHeader("已加载场景");
            panel.Add(header);

            // 场景列表
            mSceneListView = new ListView();
            mSceneListView.makeItem = MakeSceneItem;
            mSceneListView.bindItem = BindSceneItem;
            mSceneListView.fixedItemHeight = 56;
            mSceneListView.selectionType = SelectionType.Single;
            mSceneListView.selectionChanged += OnSceneSelectionChanged;
            mSceneListView.style.flexGrow = 1;
            panel.Add(mSceneListView);

            return panel;
        }

        private VisualElement CreateRightPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("right-panel");
            panel.style.flexGrow = 1;

            // 空状态
            mEmptyState = CreateEmptyState("🎬", "选择一个场景查看详情", "在左侧列表中选择场景");
            mEmptyState.style.display = DisplayStyle.Flex;
            panel.Add(mEmptyState);

            // 详情面板
            mDetailPanel = new VisualElement();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.display = DisplayStyle.None;
            panel.Add(mDetailPanel);

            BuildDetailPanel();

            return panel;
        }

        private void BuildDetailPanel()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 20;
            scrollView.style.paddingRight = 20;
            scrollView.style.paddingTop = 20;
            mDetailPanel.Add(scrollView);

            // 标题区域
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 20;
            scrollView.Add(titleRow);

            var iconBg = new VisualElement();
            iconBg.style.width = 48;
            iconBg.style.height = 48;
            iconBg.style.borderTopLeftRadius = 10;
            iconBg.style.borderTopRightRadius = 10;
            iconBg.style.borderBottomLeftRadius = 10;
            iconBg.style.borderBottomRightRadius = 10;
            iconBg.style.backgroundColor = new StyleColor(new Color(0.3f, 0.5f, 0.4f, 0.3f));
            iconBg.style.alignItems = Align.Center;
            iconBg.style.justifyContent = Justify.Center;
            iconBg.style.marginRight = 16;
            titleRow.Add(iconBg);

            var icon = new Label(KitIcons.SCENEKIT);
            icon.style.fontSize = 24;
            iconBg.Add(icon);

            var titleBox = new VisualElement();
            titleRow.Add(titleBox);

            mDetailSceneName = new Label("场景名称");
            mDetailSceneName.style.fontSize = 18;
            mDetailSceneName.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailSceneName.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            titleBox.Add(mDetailSceneName);

            mDetailBuildIndex = new Label("Build Index: -1");
            mDetailBuildIndex.style.fontSize = 12;
            mDetailBuildIndex.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            mDetailBuildIndex.style.marginTop = 4;
            titleBox.Add(mDetailBuildIndex);

            // 状态信息卡片
            var (stateCard, stateContent) = CreateCard("状态信息", "📊");
            scrollView.Add(stateCard);

            var (stateRow, stateValue) = CreateInfoRow("状态");
            mDetailState = stateValue;
            stateContent.Add(stateRow);

            var (progressRow, progressValue) = CreateInfoRow("加载进度");
            mDetailProgress = progressValue;
            stateContent.Add(progressRow);

            var (modeRow, modeValue) = CreateInfoRow("加载模式");
            mDetailLoadMode = modeValue;
            stateContent.Add(modeRow);

            var (suspendedRow, suspendedValue) = CreateInfoRow("暂停状态");
            mDetailIsSuspended = suspendedValue;
            stateContent.Add(suspendedRow);

            var (preloadedRow, preloadedValue) = CreateInfoRow("预加载");
            mDetailIsPreloaded = preloadedValue;
            stateContent.Add(preloadedRow);

            var (dataRow, dataValue) = CreateInfoRow("场景数据");
            mDetailHasData = dataValue;
            stateContent.Add(dataRow);

            // 操作按钮
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 20;
            scrollView.Add(buttonRow);

            var unloadBtn = CreateDangerButton("🗑️ 卸载场景", UnloadSelectedScene);
            buttonRow.Add(unloadBtn);

            var activateBtn = CreateSecondaryButton("✅ 设为活动场景", SetSelectedSceneActive);
            activateBtn.style.marginLeft = 8;
            buttonRow.Add(activateBtn);
        }

        private VisualElement MakeSceneItem()
        {
            var item = new VisualElement();
            item.AddToClassList("list-item");
            item.style.minHeight = 52;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;

            // 状态指示器
            var indicator = new VisualElement();
            indicator.AddToClassList("list-item-indicator");
            indicator.name = "indicator";
            item.Add(indicator);

            // 内容区域
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.justifyContent = Justify.Center;
            item.Add(content);

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;
            content.Add(topRow);

            var sceneLabel = new Label();
            sceneLabel.name = "scene-label";
            sceneLabel.style.fontSize = 13;
            sceneLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            sceneLabel.style.flexGrow = 1;
            topRow.Add(sceneLabel);

            var stateBadge = new Label();
            stateBadge.name = "state-badge";
            stateBadge.style.fontSize = 10;
            stateBadge.style.paddingLeft = 6;
            stateBadge.style.paddingRight = 6;
            stateBadge.style.paddingTop = 2;
            stateBadge.style.paddingBottom = 2;
            stateBadge.style.borderTopLeftRadius = 4;
            stateBadge.style.borderTopRightRadius = 4;
            stateBadge.style.borderBottomLeftRadius = 4;
            stateBadge.style.borderBottomRightRadius = 4;
            topRow.Add(stateBadge);

            var infoLabel = new Label();
            infoLabel.name = "info-label";
            infoLabel.style.fontSize = 11;
            infoLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            infoLabel.style.marginTop = 4;
            content.Add(infoLabel);

            // 活动场景标记
            var activeLabel = new Label();
            activeLabel.name = "active-label";
            activeLabel.AddToClassList("list-item-count");
            item.Add(activeLabel);

            return item;
        }

        private void BindSceneItem(VisualElement element, int index)
        {
            var scene = mScenes[index];

            var indicator = element.Q<VisualElement>("indicator");
            var sceneLabel = element.Q<Label>("scene-label");
            var stateBadge = element.Q<Label>("state-badge");
            var infoLabel = element.Q<Label>("info-label");
            var activeLabel = element.Q<Label>("active-label");

            // 场景名称
            sceneLabel.text = scene.SceneName;

            // 状态指示器
            indicator.RemoveFromClassList("active");
            indicator.RemoveFromClassList("inactive");
            indicator.AddToClassList(scene.State == SceneState.Loaded ? "active" : "inactive");

            // 状态徽章
            stateBadge.text = GetStateText(scene.State);
            SetStateBadgeStyle(stateBadge, scene.State);

            // 信息行
            var infoText = $"Build Index: {scene.BuildIndex}";
            if (scene.IsSuspended) infoText += " | 已暂停";
            if (scene.IsPreloaded) infoText += " | 预加载";
            infoLabel.text = infoText;

            // 活动场景标记
            activeLabel.text = scene.IsActive ? "⭐ 活动" : "";
            activeLabel.style.display = scene.IsActive ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string GetStateText(SceneState state) => state switch
        {
            SceneState.None => "无",
            SceneState.Loading => "加载中",
            SceneState.Loaded => "已加载",
            SceneState.Unloading => "卸载中",
            SceneState.Unloaded => "已卸载",
            _ => "未知"
        };

        private static void SetStateBadgeStyle(Label badge, SceneState state)
        {
            Color bgColor;
            Color textColor;

            switch (state)
            {
                case SceneState.Loaded:
                    bgColor = new Color(0.2f, 0.5f, 0.3f);
                    textColor = new Color(0.7f, 1f, 0.7f);
                    break;
                case SceneState.Loading:
                    bgColor = new Color(0.5f, 0.4f, 0.2f);
                    textColor = new Color(1f, 0.9f, 0.6f);
                    break;
                case SceneState.Unloading:
                    bgColor = new Color(0.5f, 0.3f, 0.2f);
                    textColor = new Color(1f, 0.7f, 0.6f);
                    break;
                default:
                    bgColor = new Color(0.3f, 0.3f, 0.3f);
                    textColor = new Color(0.7f, 0.7f, 0.7f);
                    break;
            }

            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.color = new StyleColor(textColor);
        }

        #endregion

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
