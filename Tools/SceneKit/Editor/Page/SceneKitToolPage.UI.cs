#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit 页面 UI 结构。
    /// </summary>
    public partial class SceneKitToolPage
    {
        private VisualElement CreateLeftPanel()
        {
            var (panel, body) = CreateKitSectionPanel("已加载场景", "展示当前 SceneKit 或 Unity 已加载的场景列表。", KitIcons.SCENEKIT);
            panel.style.flexGrow = 1;

            mSceneListView = new ListView
            {
                makeItem = MakeSceneItem,
                bindItem = BindSceneItem,
                fixedItemHeight = 56,
                selectionType = SelectionType.Single
            };
            mSceneListView.AddToClassList("yoki-scene-list");
            mSceneListView.style.flexGrow = 1;

#if UNITY_2022_1_OR_NEWER
            mSceneListView.selectionChanged += OnSceneSelectionChanged;
#else
            mSceneListView.onSelectionChange += OnSceneSelectionChanged;
#endif

            body.Add(mSceneListView);
            return panel;
        }

        private VisualElement CreateRightPanel()
        {
            var (panel, body) = CreateKitSectionPanel("场景详情", "查看状态、加载模式、预加载与挂起信息。", KitIcons.DOCUMENTATION);
            panel.style.flexGrow = 1;

            mEmptyState = CreateEmptyState(KitIcons.SCENEKIT, "选择一个场景查看详情", "右侧会展示状态信息与常用操作。");
            mEmptyState.style.display = DisplayStyle.Flex;
            body.Add(mEmptyState);

            mDetailPanel = new VisualElement();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.display = DisplayStyle.None;
            body.Add(mDetailPanel);

            BuildDetailPanel();
            return panel;
        }

        private void BuildDetailPanel()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mDetailPanel.Add(scrollView);

            var titleRow = CreateRow();
            titleRow.style.marginBottom = Spacing.XL;
            scrollView.Add(titleRow);

            var iconWrap = new VisualElement();
            iconWrap.style.width = 48;
            iconWrap.style.height = 48;
            iconWrap.style.marginRight = Spacing.LG;
            iconWrap.style.alignItems = Align.Center;
            iconWrap.style.justifyContent = Justify.Center;
            iconWrap.style.backgroundColor = new StyleColor(Colors.WorkbenchPrimarySoft);
            iconWrap.style.borderTopLeftRadius = 12;
            iconWrap.style.borderTopRightRadius = 12;
            iconWrap.style.borderBottomLeftRadius = 12;
            iconWrap.style.borderBottomRightRadius = 12;
            titleRow.Add(iconWrap);

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.SCENEKIT) };
            icon.style.width = 24;
            icon.style.height = 24;
            iconWrap.Add(icon);

            var titleBox = new VisualElement();
            titleBox.style.flexGrow = 1;
            titleRow.Add(titleBox);

            mDetailSceneName = new Label("场景名称");
            mDetailSceneName.style.fontSize = 18;
            mDetailSceneName.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailSceneName.style.color = new StyleColor(Colors.TextPrimary);
            titleBox.Add(mDetailSceneName);

            mDetailBuildIndex = new Label("Build Index: -1");
            mDetailBuildIndex.style.fontSize = 12;
            mDetailBuildIndex.style.marginTop = Spacing.XS;
            mDetailBuildIndex.style.color = new StyleColor(Colors.TextSecondary);
            titleBox.Add(mDetailBuildIndex);

            var (stateCard, stateBody) = CreateCard("状态信息", KitIcons.CHART);
            scrollView.Add(stateCard);

            stateBody.Add(CreateDetailInfoRow("状态", out mDetailState));
            stateBody.Add(CreateDetailInfoRow("加载进度", out mDetailProgress));
            stateBody.Add(CreateDetailInfoRow("加载模式", out mDetailLoadMode));
            stateBody.Add(CreateDetailInfoRow("暂停状态", out mDetailIsSuspended));
            stateBody.Add(CreateDetailInfoRow("预加载", out mDetailIsPreloaded));
            stateBody.Add(CreateDetailInfoRow("场景数据", out mDetailHasData));

            var buttonRow = CreateRow();
            buttonRow.style.marginTop = Spacing.XL;
            scrollView.Add(buttonRow);

            var unloadButton = CreateActionButtonWithIcon(KitIcons.DELETE, "卸载场景", UnloadSelectedScene, true);
            buttonRow.Add(unloadButton);

            var activateButton = CreateActionButtonWithIcon(KitIcons.SUCCESS, "设为活动场景", SetSelectedSceneActive, false);
            activateButton.style.marginLeft = Spacing.SM;
            buttonRow.Add(activateButton);
        }

        private VisualElement CreateDetailInfoRow(string labelText, out Label valueLabel)
        {
            var (row, value) = CreateInfoRow(labelText);
            valueLabel = value;
            return row;
        }

        private VisualElement MakeSceneItem()
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-scene-item");

            var indicator = new VisualElement();
            indicator.AddToClassList("list-item-indicator");
            indicator.name = "indicator";
            item.Add(indicator);

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

            var activeLabel = new Label();
            activeLabel.name = "active-label";
            activeLabel.AddToClassList("list-item-count");
            item.Add(activeLabel);

            return item;
        }

        private void BindSceneItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mScenes.Count)
            {
                return;
            }

            SceneInfo scene = mScenes[index];

            var indicator = element.Q<VisualElement>("indicator");
            var sceneLabel = element.Q<Label>("scene-label");
            var stateBadge = element.Q<Label>("state-badge");
            var infoLabel = element.Q<Label>("info-label");
            var activeLabel = element.Q<Label>("active-label");

            sceneLabel.text = scene.SceneName;

            indicator.RemoveFromClassList("active");
            indicator.RemoveFromClassList("inactive");
            indicator.AddToClassList(scene.State == SceneState.Loaded ? "active" : "inactive");

            stateBadge.text = GetStateText(scene.State);
            SetStateBadgeStyle(stateBadge, scene.State);

            string infoText = $"Build Index: {scene.BuildIndex}";
            if (scene.IsSuspended)
            {
                infoText += " | 已暂停";
            }

            if (scene.IsPreloaded)
            {
                infoText += " | 预加载";
            }

            infoLabel.text = infoText;

            activeLabel.text = scene.IsActive ? "活动" : string.Empty;
            activeLabel.style.display = scene.IsActive ? DisplayStyle.Flex : DisplayStyle.None;

            element.EnableInClassList("yoki-scene-item--active", scene.IsActive);
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
    }
}
#endif
