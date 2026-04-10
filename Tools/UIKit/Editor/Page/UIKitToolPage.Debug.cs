using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 工具页中的运行时监控子页。
    /// 用于观察当前 UIKit 的全局运行时状态，包括活动面板、面板栈、当前焦点和缓存面板。
    /// 该页只负责全局监控，不替代单个 UIPanel 的 Inspector 调试能力。
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 调试

        private bool mShowActivePanels = true;
        private bool mShowStackInfo = true;
        private bool mShowFocusInfo = true;
        private bool mShowCacheInfo;
        private bool mDebugAutoRefresh = true;
        private VisualElement mDebugContent;
        private Label mDebugActivePanelMetric;
        private Label mDebugStackMetric;
        private Label mDebugFocusMetric;
        private Label mDebugCacheMetric;

        #endregion

        #region 调试 UI

        private void BuildDebugUI(VisualElement container)
        {
            var toolbar = CreateToolbar();
            toolbar.AddToClassList("yoki-ui-debug-toolbar");
            container.Add(toolbar);

            var filterGroup = new VisualElement();
            filterGroup.AddToClassList("yoki-ui-debug-toolbar__filters");
            toolbar.Add(filterGroup);

            filterGroup.Add(CreateDebugToggleButton("活动面板", mShowActivePanels, value =>
            {
                mShowActivePanels = value;
                RefreshDebugContent();
            }));
            filterGroup.Add(CreateDebugToggleButton("面板栈", mShowStackInfo, value =>
            {
                mShowStackInfo = value;
                RefreshDebugContent();
            }));
            filterGroup.Add(CreateDebugToggleButton("焦点", mShowFocusInfo, value =>
            {
                mShowFocusInfo = value;
                RefreshDebugContent();
            }));
            filterGroup.Add(CreateDebugToggleButton("缓存", mShowCacheInfo, value =>
            {
                mShowCacheInfo = value;
                RefreshDebugContent();
            }));

            toolbar.Add(CreateToolbarSpacer());

            var actionGroup = new VisualElement();
            actionGroup.AddToClassList("yoki-ui-debug-toolbar__actions");
            toolbar.Add(actionGroup);

            var statusChip = CreateDebugStatusChip();
            actionGroup.Add(statusChip);

            var autoRefreshToggle = CreateModernToggle("自动刷新", mDebugAutoRefresh, value =>
            {
                mDebugAutoRefresh = value;
                UpdateDebugStatusChip(statusChip);
            });
            autoRefreshToggle.AddToClassList("yoki-ui-debug-toolbar__toggle");
            actionGroup.Add(autoRefreshToggle);

            var refreshBtn = new Button(RefreshDebugContent) { text = "刷新" };
            refreshBtn.AddToClassList("yoki-ui-button");
            refreshBtn.AddToClassList("yoki-ui-debug-toolbar__refresh");
            actionGroup.Add(refreshBtn);

            var metricStrip = CreateKitMetricStrip();
            var (activeCard, activeValue) = CreateKitMetricCard("活动面板", "0", "当前可见与隐藏的面板数量");
            var (stackCard, stackValue) = CreateKitMetricCard("面板栈", "0", "当前已注册的栈数量");
            var (focusCard, focusValue) = CreateKitMetricCard("当前焦点", "-", "输入系统当前焦点对象");
            var (cacheCard, cacheValue) = CreateKitMetricCard("缓存面板", "0", "预加载缓存中持有的面板数量");
            metricStrip.Add(activeCard);
            metricStrip.Add(stackCard);
            metricStrip.Add(focusCard);
            metricStrip.Add(cacheCard);
            container.Add(metricStrip);

            mDebugActivePanelMetric = activeValue;
            mDebugStackMetric = stackValue;
            mDebugFocusMetric = focusValue;
            mDebugCacheMetric = cacheValue;

            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-ui-debug-content");
            scrollView.style.flexGrow = 1;
            container.Add(scrollView);

            mDebugContent = new VisualElement();
            mDebugContent.style.flexGrow = 1;
            scrollView.Add(mDebugContent);

            RefreshDebugContent();
        }

        private Button CreateDebugToggleButton(string text, bool initialValue, Action<bool> onChanged)
        {
            var btn = new Button { text = text };
            btn.AddToClassList("yoki-ui-debug-toggle");

            void UpdateStyle(bool isActive)
            {
                btn.RemoveFromClassList("yoki-ui-debug-toggle--active");
                btn.RemoveFromClassList("yoki-ui-debug-toggle--inactive");
                btn.AddToClassList(isActive ? "yoki-ui-debug-toggle--active" : "yoki-ui-debug-toggle--inactive");
            }

            UpdateStyle(initialValue);
            bool currentValue = initialValue;

            btn.clicked += () =>
            {
                currentValue = !currentValue;
                UpdateStyle(currentValue);
                onChanged?.Invoke(currentValue);
            };

            return btn;
        }

        private void RefreshDebugContent()
        {
            if (mDebugContent == null)
            {
                return;
            }

            mDebugContent.Clear();

            if (!EditorApplication.isPlaying)
            {
                mDebugActivePanelMetric.text = "0";
                mDebugStackMetric.text = "0";
                mDebugFocusMetric.text = "无";
                mDebugCacheMetric.text = "0";
                mDebugContent.Add(CreateHelpBox("运行时监控用于观察当前 UIKit 的全局面板栈、焦点和缓存状态。请进入 PlayMode 后查看实时数据；单个 UIPanel 的绑定树与校验结果请在 Inspector 中查看。"));
                return;
            }

            var activePanels = GetActivePanels();
            var stackNames = UIKit.GetAllStackNames();
            var cachedPanels = UIKit.GetCachedPanels();
            var uiRoot = UIRoot.ExistingInstance;
            var focusName = uiRoot != default && uiRoot.CurrentFocus != default
                ? uiRoot.CurrentFocus.name
                : "无";

            mDebugActivePanelMetric.text = activePanels.Count.ToString();
            mDebugStackMetric.text = stackNames.Count.ToString();
            mDebugFocusMetric.text = focusName;
            mDebugCacheMetric.text = cachedPanels.Count.ToString();

            if (mShowActivePanels)
            {
                DrawDebugActivePanels(activePanels);
            }

            if (mShowStackInfo)
            {
                DrawDebugStackInfo(stackNames);
            }

            if (mShowFocusInfo)
            {
                DrawDebugFocusInfo();
            }

            if (mShowCacheInfo)
            {
                DrawDebugCacheInfo(cachedPanels);
            }
        }

        #endregion

        #region 调试内容绘制

        private void DrawDebugActivePanels(IReadOnlyList<IPanel> panels)
        {
            var (panel, body) = CreateKitSectionPanel("活动面板", "查看当前已激活的 UI 面板状态与快捷操作。", KitIcons.CLIPBOARD);
            mDebugContent.Add(panel);

            if (panels.Count == 0)
            {
                body.Add(new Label("当前没有活动面板。") { style = { color = new StyleColor(Colors.TextTertiary) } });
                return;
            }

            foreach (var panelInfo in panels)
            {
                body.Add(CreateDebugPanelRow(panelInfo));
            }
        }

        private VisualElement CreateDebugPanelRow(IPanel panel)
        {
            var row = new VisualElement();
            row.AddToClassList("yoki-ui-debug-panel-row");

            var panelName = panel.GetType().Name;
            var state = panel.State.ToString();
            var level = panel.Handler?.Level.ToString() ?? "Unknown";

            var nameLabel = new Label(panelName);
            nameLabel.AddToClassList("yoki-ui-debug-panel-row__name");
            row.Add(nameLabel);

            var stateLabel = new Label($"[{state}]");
            stateLabel.AddToClassList("yoki-ui-debug-panel-row__state");
            stateLabel.style.color = new StyleColor(GetStateColor(panel.State));
            row.Add(stateLabel);

            var levelLabel = new Label($"层级: {level}");
            levelLabel.AddToClassList("yoki-ui-debug-panel-row__level");
            row.Add(levelLabel);

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            if (panel.State == PanelState.Open)
            {
                row.Add(CreateSmallButton("隐藏", panel.Hide));
                row.Add(CreateSmallButton("关闭", panel.Close));
            }
            else if (panel.State == PanelState.Hide)
            {
                row.Add(CreateSmallButton("显示", panel.Show));
                row.Add(CreateSmallButton("关闭", panel.Close));
            }

            if (panel is MonoBehaviour mb && mb != null)
            {
                row.Add(CreateSmallButton("选择", () =>
                {
                    Selection.activeGameObject = mb.gameObject;
                    EditorGUIUtility.PingObject(mb.gameObject);
                }));
            }

            return row;
        }

        private void DrawDebugStackInfo(IReadOnlyCollection<string> stackNames)
        {
            var (panel, body) = CreateKitSectionPanel("面板栈", "查看各栈的深度、栈顶面板与快捷操作。", KitIcons.STACK);
            mDebugContent.Add(panel);

            if (stackNames.Count == 0)
            {
                body.Add(new Label("当前没有栈信息。") { style = { color = new StyleColor(Colors.TextTertiary) } });
                return;
            }

            foreach (var stackName in stackNames)
            {
                var depth = UIKit.GetStackDepth(stackName);
                var topPanel = UIKit.PeekPanel(stackName);
                var topPanelName = topPanel?.GetType().Name ?? "空";

                var row = new VisualElement();
                row.AddToClassList("yoki-ui-row");
                row.AddToClassList("yoki-ui-row--padded");

                var stackLabel = new Label($"栈[{stackName}]");
                stackLabel.AddToClassList("yoki-ui-label--fixed-width");
                row.Add(stackLabel);

                var depthLabel = new Label($"深度: {depth}");
                depthLabel.AddToClassList("yoki-ui-label--small-width");
                row.Add(depthLabel);

                var topLabel = new Label($"栈顶: {topPanelName}");
                topLabel.AddToClassList("yoki-ui-label--grow");
                row.Add(topLabel);

                if (depth > 0)
                {
                    row.Add(CreateSmallButton("Pop", () => UIKit.PopPanel(stackName)));
                    row.Add(CreateSmallButton("清空", () => UIKit.ClearStack(stackName)));
                }

                body.Add(row);
            }
        }

        private void DrawDebugFocusInfo()
        {
            var (panel, body) = CreateKitSectionPanel("焦点信息", "查看当前焦点对象与输入模式。", KitIcons.TARGET);
            mDebugContent.Add(panel);

            var uiRoot = UIRoot.ExistingInstance;
            if (uiRoot == default)
            {
                body.Add(new Label("UIRoot 尚未初始化。") { style = { color = new StyleColor(Colors.TextTertiary) } });
                return;
            }

            var currentFocus = uiRoot.CurrentFocus;
            var focusName = currentFocus != default ? currentFocus.name : "无";
            var inputMode = uiRoot.CurrentInputMode.ToString();

            var (focusRow, _) = CreateInfoRow("当前焦点:", focusName);
            body.Add(focusRow);

            var (modeRow, _) = CreateInfoRow("输入模式:", inputMode);
            body.Add(modeRow);

            if (currentFocus != null)
            {
                var btnRow = new VisualElement();
                btnRow.AddToClassList("yoki-ui-row");
                btnRow.AddToClassList("yoki-ui-row--justified-end");

                btnRow.Add(CreateSmallButton("选择焦点对象", () =>
                {
                    Selection.activeGameObject = currentFocus.gameObject;
                    EditorGUIUtility.PingObject(currentFocus.gameObject);
                }));

                body.Add(btnRow);
            }
        }

        private void DrawDebugCacheInfo(IReadOnlyList<IPanel> cachedPanels)
        {
            var (panel, body) = CreateKitSectionPanel("缓存信息", "查看预加载缓存中的面板与清理入口。", KitIcons.CACHE);
            mDebugContent.Add(panel);

            var cacheCount = cachedPanels.Count;
            var maxCapacity = UIKit.GetCacheCapacity();

            var (countRow, _) = CreateInfoRow("缓存数量:", $"{cacheCount} / {maxCapacity}");
            body.Add(countRow);

            if (cacheCount == 0)
            {
                body.Add(new Label("当前没有缓存面板。")
                {
                    style =
                    {
                        color = new StyleColor(Colors.TextTertiary),
                        marginTop = Spacing.SM
                    }
                });
                return;
            }

            foreach (var cachedPanel in cachedPanels)
            {
                var row = new VisualElement();
                row.AddToClassList("yoki-ui-row");
                row.AddToClassList("yoki-ui-row--tight");

                var panelLabel = new Label($"- {cachedPanel.GetType().Name}");
                panelLabel.AddToClassList("yoki-ui-label--grow");
                row.Add(panelLabel);

                if (cachedPanel is MonoBehaviour mb && mb != null)
                {
                    row.Add(CreateSmallButton("选择", () => Selection.activeGameObject = mb.gameObject));
                }

                body.Add(row);
            }

            var clearBtn = CreateDangerButton("清空缓存", UIKit.ClearAllPreloadedCache);
            clearBtn.AddToClassList("yoki-ui-margin-top");
            body.Add(clearBtn);
        }

        #endregion

        #region 调试辅助方法

        private List<IPanel> GetActivePanels()
        {
            var result = new List<IPanel>();

            if (!EditorApplication.isPlaying)
            {
                return result;
            }

            var uiRoot = UIRoot.ExistingInstance;
            if (uiRoot == null)
            {
                return result;
            }

            var panels = uiRoot.GetComponentsInChildren<UIPanel>(true);
            foreach (var panel in panels)
            {
                if (panel.State != PanelState.Close)
                {
                    result.Add(panel);
                }
            }

            return result;
        }

        private static Color GetStateColor(PanelState state) => state switch
        {
            PanelState.Open => Colors.StatusSuccess,
            PanelState.Hide => Colors.StatusWarning,
            PanelState.Close => Colors.TextTertiary,
            _ => Color.white
        };

        private VisualElement CreateDebugStatusChip()
        {
            var chip = new VisualElement();
            chip.AddToClassList("yoki-ui-debug-toolbar__status");

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.REFRESH) };
            icon.AddToClassList("yoki-ui-debug-toolbar__status-icon");
            icon.tintColor = Colors.BrandPrimary;
            chip.Add(icon);

            var label = new Label();
            label.AddToClassList("yoki-ui-debug-toolbar__status-label");
            chip.Add(label);

            UpdateDebugStatusChip(chip);
            return chip;
        }

        private void UpdateDebugStatusChip(VisualElement chip)
        {
            if (chip == null)
            {
                return;
            }

            var label = chip.Q<Label>();
            if (label != null)
            {
                label.text = mDebugAutoRefresh ? "实时刷新已开启" : "实时刷新已暂停";
            }

            chip.RemoveFromClassList("yoki-ui-debug-toolbar__status--active");
            chip.RemoveFromClassList("yoki-ui-debug-toolbar__status--inactive");
            chip.AddToClassList(mDebugAutoRefresh
                ? "yoki-ui-debug-toolbar__status--active"
                : "yoki-ui-debug-toolbar__status--inactive");
        }

        #endregion
    }
}
