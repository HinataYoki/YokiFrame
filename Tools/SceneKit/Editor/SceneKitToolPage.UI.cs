#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit 工具页面 - UI 构建
    /// </summary>
    public partial class SceneKitToolPage
    {
        #region UI 构建

        private VisualElement CreateLeftPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("left-panel");

            // 头部
            var header = CreateSectionHeader("已加载场景");
            panel.Add(header);

            // 场景列表
            mSceneListView = new ListView();
            mSceneListView.AddToClassList("yoki-scene-list");
            mSceneListView.makeItem = MakeSceneItem;
            mSceneListView.bindItem = BindSceneItem;
            mSceneListView.fixedItemHeight = 56;
            mSceneListView.selectionType = SelectionType.Single;
#if UNITY_2022_1_OR_NEWER
            mSceneListView.selectionChanged += OnSceneSelectionChanged;
#else
            mSceneListView.onSelectionChange += OnSceneSelectionChanged;
#endif
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
            mEmptyState = CreateEmptyState(KitIcons.SCENEKIT, "选择一个场景查看详情", "在左侧列表中选择场景");
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
            scrollView.style.paddingLeft = Spacing.XL;
            scrollView.style.paddingRight = Spacing.XL;
            scrollView.style.paddingTop = Spacing.XL;
            mDetailPanel.Add(scrollView);

            // 标题区域
            var titleRow = CreateRow();
            titleRow.style.marginBottom = Spacing.XL;
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
            iconBg.style.marginRight = Spacing.LG;
            titleRow.Add(iconBg);

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.SCENEKIT) };
            icon.style.width = 24;
            icon.style.height = 24;
            iconBg.Add(icon);

            var titleBox = new VisualElement();
            titleRow.Add(titleBox);

            mDetailSceneName = new Label("场景名称");
            mDetailSceneName.style.fontSize = 18;
            mDetailSceneName.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailSceneName.style.color = new StyleColor(Colors.TextPrimary);
            titleBox.Add(mDetailSceneName);

            mDetailBuildIndex = new Label("Build Index: -1");
            mDetailBuildIndex.style.fontSize = 12;
            mDetailBuildIndex.style.color = new StyleColor(Colors.TextSecondary);
            mDetailBuildIndex.style.marginTop = Spacing.XS;
            titleBox.Add(mDetailBuildIndex);

            // 状态信息卡片
            var (stateCard, stateContent) = CreateCard("状态信息", KitIcons.CHART);
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
            var buttonRow = CreateRow();
            buttonRow.style.marginTop = Spacing.XL;
            scrollView.Add(buttonRow);

            var unloadBtn = CreateActionButtonWithIcon(KitIcons.DELETE, "卸载场景", UnloadSelectedScene, true);
            buttonRow.Add(unloadBtn);

            var activateBtn = CreateActionButtonWithIcon(KitIcons.SUCCESS, "设为活动场景", SetSelectedSceneActive, false);
            activateBtn.style.marginLeft = Spacing.SM;
            buttonRow.Add(activateBtn);
        }

        private VisualElement MakeSceneItem()
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-scene-item");

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

            var topRow = CreateRow();
            content.Add(topRow);

            var sceneLabel = new Label();
            sceneLabel.name = "scene-label";
            sceneLabel.AddToClassList("yoki-scene-item__name");
            topRow.Add(sceneLabel);

            var stateBadge = new Label();
            stateBadge.name = "state-badge";
            stateBadge.style.fontSize = 10;
            stateBadge.style.paddingLeft = Spacing.SM;
            stateBadge.style.paddingRight = Spacing.SM;
            stateBadge.style.paddingTop = 2;
            stateBadge.style.paddingBottom = 2;
            stateBadge.style.borderTopLeftRadius = Radius.MD;
            stateBadge.style.borderTopRightRadius = Radius.MD;
            stateBadge.style.borderBottomLeftRadius = Radius.MD;
            stateBadge.style.borderBottomRightRadius = Radius.MD;
            topRow.Add(stateBadge);

            var infoLabel = new Label();
            infoLabel.name = "info-label";
            infoLabel.AddToClassList("yoki-scene-item__path");
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
            activeLabel.text = scene.IsActive ? "活动" : "";
            activeLabel.style.display = scene.IsActive ? DisplayStyle.Flex : DisplayStyle.None;
            
            // 活动场景修饰符
            element.RemoveFromClassList("yoki-scene-item--active");
            if (scene.IsActive)
            {
                element.AddToClassList("yoki-scene-item--active");
            }
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
                    bgColor = Colors.BadgeSuccess;
                    textColor = new Color(0.7f, 1f, 0.7f);
                    break;
                case SceneState.Loading:
                    bgColor = Colors.BadgeWarning;
                    textColor = new Color(1f, 0.9f, 0.6f);
                    break;
                case SceneState.Unloading:
                    bgColor = Colors.BadgeError;
                    textColor = new Color(1f, 0.7f, 0.6f);
                    break;
                default:
                    bgColor = Colors.BadgeDefault;
                    textColor = Colors.TextSecondary;
                    break;
            }

            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.color = new StyleColor(textColor);
        }

        #endregion
    }
}
#endif
