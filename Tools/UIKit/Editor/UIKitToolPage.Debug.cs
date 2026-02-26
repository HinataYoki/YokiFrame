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
    /// UIKitToolPage - 调试功能
    /// 采用响应式数据绑定，通过订阅面板事件实现自动更新
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

        #endregion

        #region 调试 UI

        private void BuildDebugUI(VisualElement container)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.AddToClassList("yoki-ui-debug-toolbar");
            container.Add(toolbar);

            // 过滤按钮
            toolbar.Add(CreateDebugToggleButton("活动面板", mShowActivePanels, v => { mShowActivePanels = v; RefreshDebugContent(); }));
            toolbar.Add(CreateDebugToggleButton("堆栈", mShowStackInfo, v => { mShowStackInfo = v; RefreshDebugContent(); }));
            toolbar.Add(CreateDebugToggleButton("焦点", mShowFocusInfo, v => { mShowFocusInfo = v; RefreshDebugContent(); }));
            toolbar.Add(CreateDebugToggleButton("缓存", mShowCacheInfo, v => { mShowCacheInfo = v; RefreshDebugContent(); }));

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // 响应式提示
            var reactiveIcon = new Image { image = KitIcons.GetTexture(KitIcons.REFRESH) };
            reactiveIcon.AddToClassList("yoki-ui-icon");
            reactiveIcon.tintColor = Colors.StatusSuccess;
            reactiveIcon.tooltip = "响应式更新";
            toolbar.Add(reactiveIcon);

            // 自动刷新
            var autoRefreshToggle = CreateModernToggle("自动刷新", mDebugAutoRefresh, v => mDebugAutoRefresh = v);
            autoRefreshToggle.AddToClassList("yoki-ui-icon");
            toolbar.Add(autoRefreshToggle);

            var refreshBtn = new Button(RefreshDebugContent) { text = "刷新" };
            refreshBtn.AddToClassList("yoki-ui-button");
            toolbar.Add(refreshBtn);

            // 内容区域
            mDebugContent = new ScrollView();
            mDebugContent.AddToClassList("yoki-ui-debug-content");
            container.Add(mDebugContent);

            RefreshDebugContent();
        }

        private Button CreateDebugToggleButton(string text, bool initialValue, Action<bool> onChanged)
        {
            var btn = new Button();
            btn.text = text;
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
            if (mDebugContent == null) return;
            mDebugContent.Clear();

            if (!EditorApplication.isPlaying)
            {
                var helpBox = CreateHelpBox("请在播放模式下使用调试功能");
                mDebugContent.Add(helpBox);
                return;
            }

            if (mShowActivePanels) DrawDebugActivePanels();
            if (mShowStackInfo) DrawDebugStackInfo();
            if (mShowFocusInfo) DrawDebugFocusInfo();
            if (mShowCacheInfo) DrawDebugCacheInfo();
        }

        #endregion

        #region 调试内容绘制

        private void DrawDebugActivePanels()
        {
            var (card, body) = CreateCard("活动面板", KitIcons.CLIPBOARD);
            mDebugContent.Add(card);

            var panels = GetActivePanels();
            if (panels.Count == 0)
            {
                body.Add(new Label("无活动面板") { style = { color = new StyleColor(Colors.TextTertiary) } });
                return;
            }

            foreach (var panel in panels)
            {
                var row = CreateDebugPanelRow(panel);
                body.Add(row);
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

            var levelLabel = new Label($"Level: {level}");
            levelLabel.AddToClassList("yoki-ui-debug-panel-row__level");
            row.Add(levelLabel);

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            // 操作按钮
            if (panel.State == PanelState.Open)
            {
                row.Add(CreateSmallButton("隐藏", () => panel.Hide()));
                row.Add(CreateSmallButton("关闭", () => panel.Close()));
            }
            else if (panel.State == PanelState.Hide)
            {
                row.Add(CreateSmallButton("显示", () => panel.Show()));
                row.Add(CreateSmallButton("关闭", () => panel.Close()));
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

        private void DrawDebugStackInfo()
        {
            var (card, body) = CreateCard("面板堆栈", KitIcons.STACK);
            mDebugContent.Add(card);

            var stackNames = UIKit.GetAllStackNames();
            if (stackNames.Count == 0)
            {
                body.Add(new Label("无堆栈") { style = { color = new StyleColor(Colors.TextTertiary) } });
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

                var stackLabel = new Label($"栈 [{stackName}]");
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
            var (card, body) = CreateCard("焦点信息", KitIcons.TARGET);
            mDebugContent.Add(card);

            if (UIRoot.Instance == default)
            {
                body.Add(new Label("UIRoot 未初始化") { style = { color = new StyleColor(Colors.TextTertiary) } });
                return;
            }

            var currentFocus = UIRoot.Instance.CurrentFocus;
            var focusName = currentFocus != default ? currentFocus.name : "无";
            var inputMode = UIRoot.Instance.CurrentInputMode.ToString();

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

        private void DrawDebugCacheInfo()
        {
            var (card, body) = CreateCard("缓存信息", KitIcons.CACHE);
            mDebugContent.Add(card);

            var cachedPanels = UIKit.GetCachedPanels();
            var cacheCount = cachedPanels.Count;
            var maxCapacity = UIKit.GetCacheCapacity();

            var (countRow, _) = CreateInfoRow("缓存数量:", $"{cacheCount} / {maxCapacity}");
            body.Add(countRow);

            if (cacheCount == 0)
            {
                body.Add(new Label("无缓存面板") { style = { color = new StyleColor(Colors.TextTertiary), marginTop = Spacing.SM } });
                return;
            }

            foreach (var panel in cachedPanels)
            {
                var row = new VisualElement();
                row.AddToClassList("yoki-ui-row");
                row.AddToClassList("yoki-ui-row--tight");

                var panelLabel = new Label($"  - {panel.GetType().Name}");
                panelLabel.AddToClassList("yoki-ui-label--grow");
                row.Add(panelLabel);

                if (panel is MonoBehaviour mb && mb != null)
                {
                    row.Add(CreateSmallButton("选择", () => Selection.activeGameObject = mb.gameObject));
                }

                body.Add(row);
            }

            var clearBtn = CreateDangerButton("清空缓存", () => UIKit.ClearAllPreloadedCache());
            clearBtn.AddToClassList("yoki-ui-margin-top");
            body.Add(clearBtn);
        }

        #endregion

        #region 调试辅助方法

        private List<IPanel> GetActivePanels()
        {
            var result = new List<IPanel>();

            if (UIRoot.Instance == null) return result;

            var panels = UIRoot.Instance.GetComponentsInChildren<UIPanel>(true);
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

        #endregion
    }
}
